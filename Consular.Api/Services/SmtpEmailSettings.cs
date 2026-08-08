namespace Consular.Api.Services;

// Bound from the "Email:Smtp" configuration section (see appsettings.json / Program.cs). Left
// empty in this dev/demo docker-compose setup — there's no mail server or credentials provided
// here — which is exactly what tells SmtpEmailService to log instead of actually connecting.
public class SmtpEmailSettings
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "noreply@embassy.local";
    public string FromName { get; set; } = "Ambassade";
    public bool EnableSsl { get; set; } = true;
}
