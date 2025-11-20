using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace EnvisionAnalytics.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IConfiguration config, ILogger<EmailSender> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var host = (_config["Smtp:Host"] ?? _config["Smtp__Host"])?.Trim();
            var port = int.TryParse(_config["Smtp:Port"] ?? _config["Smtp__Port"], out var p) ? p : 587;
            var user = (_config["Smtp:User"] ?? _config["Smtp__User"])?.Trim();
            var pass = (_config["Smtp:Pass"] ?? _config["Smtp__Pass"])?.Trim();
            var from = (_config["Smtp:From"] ?? _config["Smtp__From"])?.Trim() ?? user;
            var enableSsl = bool.TryParse(_config["Smtp:EnableSsl"] ?? _config["Smtp__EnableSsl"], out var s) ? s : true;

            if (string.IsNullOrWhiteSpace(host))
            {
                _logger.LogError("SMTP host is not configured (Smtp:Host or Smtp__Host). Email cannot be sent.");
                throw new InvalidOperationException("SMTP host is not configured. Set Smtp:Host or environment variable Smtp__Host.");
            }

            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(from, from));
            msg.To.Add(MailboxAddress.Parse(email));
            msg.Subject = subject;
            var body = new BodyBuilder { HtmlBody = htmlMessage };
            msg.Body = body.ToMessageBody();
            using var client = new SmtpClient();
            try
            {
                var options = MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable;
                if (port == 465) options = MailKit.Security.SecureSocketOptions.SslOnConnect;
                else if (!enableSsl) options = MailKit.Security.SecureSocketOptions.None;

                _logger.LogInformation("Connecting to SMTP {Host}:{Port} (SSL={EnableSsl})", host, port, enableSsl);
                await client.ConnectAsync(host, port, options);

                if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
                {
                    await client.AuthenticateAsync(user, pass);
                }

                await client.SendAsync(msg);
                await client.DisconnectAsync(true);
                _logger.LogInformation("Email sent to {Email} via SMTP {Host}:{Port}", email, host, port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email} via SMTP {Host}:{Port}", email, host, port);
                throw;
            }
        }
    }
}
