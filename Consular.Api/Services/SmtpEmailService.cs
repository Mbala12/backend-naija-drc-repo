using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Consular.Api.Services;

// Sends real mail via SMTP when Email:Smtp:Host is configured (e.g. set via env vars in a real
// deployment). In this dev/demo environment nothing is configured — docker-compose.yml has no
// mail service and no credentials are ever supplied — so it just logs what it would have sent
// instead of trying (and failing) to connect. Either way, a failure here never blocks the
// underlying status transition (see DemandesController.Transition) — a notification email is a
// nice-to-have, not something that should stop a staff action from completing.
public class SmtpEmailService : IEmailService
{
    private readonly SmtpEmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpEmailSettings> settings, ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.Host))
        {
            _logger.LogInformation(
                "Email not sent (no SMTP host configured) — would have sent to {ToName} <{ToEmail}>: {Subject}\n{Body}",
                toName, toEmail, subject, body);
            return;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            message.To.Add(new MailAddress(toEmail, toName));

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = string.IsNullOrWhiteSpace(_settings.User)
                    ? null
                    : new NetworkCredential(_settings.User, _settings.Password)
            };

            await client.SendMailAsync(message, ct);
            _logger.LogInformation("Email sent to {ToName} <{ToEmail}>: {Subject}", toName, toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail} ({Subject})", toEmail, subject);
        }
    }
}
