using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Consular.Api.Auth;
using Consular.Api.Data;
using Consular.Api.Dtos;
using Consular.Api.Services;
using Consular.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UserEntity = Consular.Shared.Entities.User;

namespace Consular.Api.Controllers;

// Two separate account tables, one shared login. Applicants are the public: self-registered,
// no role, can only ever act on their own requests. Users are staff/admin: created only by an
// existing admin (see UsersController), and carry a Region that continues to govern demande-
// processing permissions exactly as it did when Applicant/User were one combined table.
//
// The User entity is aliased to UserEntity throughout: ControllerBase already exposes a `User`
// property (the ClaimsPrincipal) which would otherwise shadow the entity type name.
[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtSettings _jwt;
    private readonly ILogger<AuthController> _logger;
    private readonly PasswordHasher<Applicant> _applicantHasher = new();
    private readonly PasswordHasher<UserEntity> _userHasher = new();

    public AuthController(AppDbContext db, IOptions<JwtSettings> jwt, ILogger<AuthController> logger)
    {
        _db = db;
        _jwt = jwt.Value;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
    {
        var applicant = await _db.Applicants.FirstOrDefaultAsync(a => a.Email == dto.Email);
        if (applicant is not null && applicant.MotDePasseHash is not null)
        {
            _logger.LogWarning("Registration rejected: an account already exists for {Email}", dto.Email);
            ModelState.AddModelError(nameof(dto.Email), "Un compte existe déjà avec cette adresse email.");
            return ValidationProblem(ModelState);
        }

        var wasBeneficiaryOnly = applicant is not null;
        if (applicant is null)
        {
            if (!await _db.HasCapacityForNewAccountAsync())
            {
                _logger.LogWarning("Registration rejected: account limit reached, cannot create new applicant {Email}", dto.Email);
                ModelState.AddModelError(string.Empty, $"La limite de {AccountCapacityExtensions.MaxAccounts} comptes pour cette démo a été atteinte. Supprimez un postulant ou un utilisateur existant avant d'en ajouter un autre.");
                return ValidationProblem(ModelState);
            }
            applicant = new Applicant { Email = dto.Email };
            _db.Applicants.Add(applicant);
        }

        // applicant may already exist as a beneficiary-only record (someone submitted a demande
        // on their behalf before they ever registered) — attach credentials to that same record
        // rather than erroring, so their existing demandes are immediately visible once they log in.
        applicant.Nom = dto.Nom;
        applicant.Telephone = dto.Telephone;
        applicant.Nationalite = dto.Nationalite;
        applicant.MotDePasseHash = _applicantHasher.HashPassword(applicant, dto.Password);

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Applicant account registered for {Email}{BeneficiaryNote}",
            dto.Email, wasBeneficiaryOnly ? " — attached to an existing beneficiary-only record" : "");

        return Ok(BuildApplicantAuthResponse(applicant));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var applicant = await _db.Applicants.FirstOrDefaultAsync(a => a.Email == dto.Email);
        if (applicant is not null && applicant.MotDePasseHash is not null)
        {
            if (_applicantHasher.VerifyHashedPassword(applicant, applicant.MotDePasseHash, dto.Password) == PasswordVerificationResult.Failed)
            {
                _logger.LogWarning("Login failed for {Email}: incorrect password (applicant)", dto.Email);
                return Unauthorized(new { message = "Invalid credentials" });
            }

            _logger.LogInformation("Login succeeded for {Email} (applicant)", dto.Email);
            return Ok(BuildApplicantAuthResponse(applicant));
        }

        var user = await _db.Users.Include(u => u.Role).ThenInclude(r => r!.Permissions).FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user is not null)
        {
            if (_userHasher.VerifyHashedPassword(user, user.MotDePasseHash, dto.Password) == PasswordVerificationResult.Failed)
            {
                _logger.LogWarning("Login failed for {Email}: incorrect password (user)", dto.Email);
                return Unauthorized(new { message = "Invalid credentials" });
            }

            _logger.LogInformation("Login succeeded for {Email} (region {Region}, role={Role})", dto.Email, user.Region, user.Role?.Name);
            return Ok(BuildUserAuthResponse(user));
        }

        _logger.LogWarning("Login failed for {Email}: no account with credentials", dto.Email);
        return Unauthorized(new { message = "Invalid credentials" });
    }

    private AuthResponseDto BuildApplicantAuthResponse(Applicant applicant)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, applicant.Id.ToString()),
            new Claim(ClaimTypes.Name, applicant.Email),
            new Claim(ClaimTypes.Role, "Applicant"),
            new Claim("displayName", applicant.Nom)
        };

        return new AuthResponseDto(BuildToken(claims), applicant.Nom, "Applicant", applicant.Email, applicant.Nom, null, new List<string>(), applicant.Telephone);
    }

    // Permissions are NOT baked into the token as claims — PermissionAuthorizationHandler checks
    // them against the database on every request instead (see its own comment for why: an admin
    // revoking a permission should take effect immediately, not after the JWT's 8-hour expiry).
    // They're still returned here in the login response body so the frontend can decide what to
    // render without a first-render flash of the wrong UI.
    private AuthResponseDto BuildUserAuthResponse(UserEntity user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Role, user.Region.ToString()),
            new Claim("displayName", user.Nom)
        };

        var permissions = user.Role?.Permissions.Select(p => p.Code).ToList() ?? new List<string>();
        return new AuthResponseDto(BuildToken(claims), user.Nom, user.Region.ToString(), user.Email, user.Nom, user.Region, permissions, null);
    }

    private string BuildToken(Claim[] claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
