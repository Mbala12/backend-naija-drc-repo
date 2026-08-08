using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using Consular.Api.Auth;
using Consular.Api.Data;
using Consular.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

// The app (frontend copy, seeded data, every hand-written message below) is French-first — this
// makes .NET's own built-in DataAnnotations validation messages (e.g. [Required], [EmailAddress])
// render in French too, using the framework's own French satellite resources, rather than only
// the messages this codebase writes by hand.
var frenchCulture = new CultureInfo("fr-FR");
CultureInfo.DefaultThreadCurrentCulture = frenchCulture;
CultureInfo.DefaultThreadCurrentUICulture = frenchCulture;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration binding -------------------------------------------------
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<SmtpEmailSettings>(builder.Configuration.GetSection("Email:Smtp"));

// --- Database ---------------------------------------------------------------
// Npgsql's connection pooling + retry-on-failure covers transient network blips between
// the API pod and a managed Postgres instance (e.g. a brief failover to the replica).
var postgresConnectionString = NormalizePostgresConnectionString(builder.Configuration.GetConnectionString("Postgres")!);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(postgresConnectionString,
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(3)));

builder.Services.AddSingleton<IDocumentStorageService, LocalDocumentStorageService>();
builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<ReportAggregationService>();

// --- Auth ---------------------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ValidateLifetime = true
        };
        // AddJwtBearer swallows auth failures into a 401 by default without logging why —
        // surfacing them as Warnings makes "why did my token get rejected" debuggable.
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Consular.Api.Auth.JwtBearer");
                logger.LogWarning(context.Exception, "JWT authentication failed for {Path}", context.HttpContext.Request.Path);
                return Task.CompletedTask;
            },
            OnForbidden = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Consular.Api.Auth.JwtBearer");
                logger.LogWarning("Authenticated user {User} was forbidden from {Path} (missing required role)",
                    context.HttpContext.User.Identity?.Name ?? "unknown", context.HttpContext.Request.Path);
                return Task.CompletedTask;
            }
        };
    });
// Every authorization decision beyond "is this an Applicant vs. staff" (which still reads the
// Region-derived ClaimTypes.Role claim directly, unchanged — see [Authorize(Roles=...)] usages)
// goes through PermissionAuthorizationHandler, checking the caller's admin-configurable
// Role.Permissions from the database. One named policy per fixed permission code, seeded by the
// AddRolesAndPermissions migration / DemoDataSeeder.
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    foreach (var code in new[]
             {
                 "Demandes.View", "Demandes.Transition", "Admin.ViewData", "Admin.ManageData",
                 "Applicants.Lookup", "Users.Manage", "Reports.View"
             })
    {
        options.AddPolicy($"Permission.{code}", policy => policy.Requirements.Add(new PermissionRequirement(code)));
    }
});

// --- CORS -----------------------------------------------------------------
// The React SPA is served from a different origin in local dev (Vite on :5173).
// Behind the nginx load balancer in prod, same-origin means this mostly matters for local dev.
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
              .AllowAnyHeader()
              .AllowAnyMethod()
              // Content-Disposition isn't a CORS-safelisted response header — without this,
              // ReportsController's export filename is invisible to the frontend's fetch() in
              // local dev (cross-origin 5173 -> 8090); harmless same-origin in prod behind nginx.
              .WithExposedHeaders("Content-Disposition"));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Lets Swagger UI's "Authorize" button attach a Bearer token to requests against
    // [Authorize]-protected endpoints — without this, the login response's JWT has no
    // way to reach subsequent calls made from the UI, and every protected endpoint 401s.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT returned by POST /api/v1/auth/login (no \"Bearer \" prefix needed)."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddProblemDetails();

// --- Rate limiting --------------------------------------------------------------
// "case-tracking" covers the anonymous public case-tracking surface (track/appeal/submit-
// missing-documents on DemandesController — see [EnableRateLimiting] there). Those endpoints
// are reachable with no login, keyed off either a short guessable tracking number (the
// numéro de référence is just "DEM-{year}-{3-digit prefix}{3-digit sequence}" — NumeroReferenceGenerator)
// or a Demande's internal id, so without a limit here they're a straightforward enumeration/abuse
// target. Partitioned by client IP rather than the tracking number itself, since the whole
// point is to slow down someone trying many different numbers from the same origin.
// Document upload ([HttpPost("{id:guid}/documents")]) is deliberately NOT included here even
// though it's also [AllowAnonymous] — the staff dashboard uploads through that same endpoint
// while authenticated, and this limiter would throttle that shared usage too.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("case-tracking", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Consular.Api.RateLimiting");
        logger.LogWarning("Rate limit exceeded for {Method} {Path} from {RemoteIp}",
            context.HttpContext.Request.Method, context.HttpContext.Request.Path, context.HttpContext.Connection.RemoteIpAddress);

        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests.",
            Detail = "Too many case-tracking requests from this connection. Please wait a minute and try again."
        }, cancellationToken);
    };
});

// --- Health checks ------------------------------------------------------------
// Kubernetes/the load balancer hit /health/ready before routing traffic to a pod, and
// /health/live to decide whether to restart it. Separating the two matters: a pod with a
// slow DB should stop receiving traffic (fail readiness) without being killed (still live).
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!, name: "postgres");

// Periodically auto-rejects any demande that's sat in MISSING_DOCUMENTS past its 10-working-day
// deadline without the applicant resubmitting — see MissingDocumentsExpiryService for why this
// needs a background sweep rather than only the existing reactive deadline check.
builder.Services.AddHostedService<MissingDocumentsExpiryService>();

var app = builder.Build();
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Consular.Api.Startup");

startupLogger.LogInformation("Starting Consular.Api in {Environment} environment", app.Environment.EnvironmentName);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        var migrations = db.Database.GetMigrations().ToList();
        if (migrations.Count > 0)
        {
            startupLogger.LogInformation("Applying {Count} pending/known migration(s)", migrations.Count);
            db.Database.Migrate();
            startupLogger.LogInformation("Database migration completed successfully");
        }
        else
        {
            db.Database.EnsureCreated();
            startupLogger.LogWarning("No migrations found — database schema was created via EnsureCreated instead");
        }

        // Always call Seed() — every lookup it seeds (regions, types de service, statuts,
        // applicants, users) is added by a per-row existence check keyed on its natural key
        // (Code/Email), so re-running this on every boot is a no-op once everything already
        // exists, but it also means a code change that adds a new lookup row (e.g. a new Statut)
        // reaches an already-seeded database on its next restart instead of silently never
        // arriving. Only the demo Demandes are gated on the table being empty (see
        // DemoDataSeeder.Seed) — those are fixture data, not lookups, and shouldn't reappear
        // after being edited or deleted.
        var demoSeeder = new DemoDataSeeder();
        demoSeeder.Seed(db);
        startupLogger.LogInformation("Seeded/verified demo data for regions, services, statuses, applicants, users and requests");
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "Database initialization failed — the application cannot start");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- Forwarded headers ---------------------------------------------------------
// Real traffic never reaches Kestrel directly — it goes through the frontend's nginx
// container (see nginx.conf), which proxies /api/ and sets X-Forwarded-For. Without this,
// HttpContext.Connection.RemoteIpAddress is always nginx's own container IP for every
// request, which defeats per-IP rate limiting (see "case-tracking" below): every visitor
// would share one bucket instead of getting their own. KnownNetworks/KnownProxies are
// cleared rather than pinned to nginx's container IP because Docker assigns that IP
// dynamically (it can change across restarts/recreates) — acceptable here since the
// backend's compose network is private and only meant to be reached through that one
// proxy. Must run before anything that reads RemoteIpAddress, so it's the first
// middleware in the pipeline.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// --- Global exception handling ------------------------------------------------
// Anything that escapes a controller action (a bug, an unexpected DB/broker failure, etc.)
// lands here: logged once at Error with the full stack trace, and turned into a clean
// ProblemDetails response instead of leaking a raw 500 with no explanation in the logs.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Consular.Api.UnhandledException");
        logger.LogError(exceptionFeature?.Error, "Unhandled exception while processing {Method} {Path}",
            context.Request.Method, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = app.Environment.IsDevelopment() ? exceptionFeature?.Error.ToString() : null
        });
    });
});

// --- Request logging ------------------------------------------------------------
// One Info line per request (method, path, status, duration) and a Warning line for any
// 4xx/5xx — gives a readable operational trail without turning on EF Core's verbose
// per-SQL-statement logging (that's dialed down to Warning in appsettings.json).
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Consular.Api.Requests");
    var stopwatch = Stopwatch.StartNew();
    await next();
    stopwatch.Stop();

    var statusCode = context.Response.StatusCode;
    var message = "{Method} {Path} responded {StatusCode} in {ElapsedMs}ms";
    if (statusCode >= 500)
    {
        logger.LogError(message, context.Request.Method, context.Request.Path, statusCode, stopwatch.ElapsedMilliseconds);
    }
    else if (statusCode >= 400)
    {
        logger.LogWarning(message, context.Request.Method, context.Request.Path, statusCode, stopwatch.ElapsedMilliseconds);
    }
    else
    {
        logger.LogInformation(message, context.Request.Method, context.Request.Path, statusCode, stopwatch.ElapsedMilliseconds);
    }
});

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false }); // process is up, nothing more
app.MapHealthChecks("/health/ready"); // runs all registered checks (DB)

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() => startupLogger.LogInformation("Consular.Api started and ready to accept requests"));
lifetime.ApplicationStopping.Register(() => startupLogger.LogInformation("Consular.Api is shutting down"));

app.Run();

// Render's managed Postgres (and most cloud providers — Heroku, Railway, etc.) hand out a
// connection string as a "postgres://user:pass@host:port/db" URI, but Npgsql's UseNpgsql only
// understands ADO.NET keyword=value format ("Host=...;Port=...;..."), the shape docker-compose's
// ConnectionStrings__Postgres already uses — so a raw URI would fail to parse at startup. This
// leaves the local/docker-compose keyword-format string untouched and only converts the URI
// shape, adding SslMode=Require since Render (and most managed Postgres) requires TLS on its
// external connection.
static string NormalizePostgresConnectionString(string raw)
{
    if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
        !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return raw;
    }

    var uri = new Uri(raw);
    var userInfo = uri.UserInfo.Split(':', 2);
    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
        SslMode = SslMode.Require
    };
    return builder.ConnectionString;
}
