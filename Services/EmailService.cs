using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Raven.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IConfiguration configuration,
            ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
        {
            var host = _configuration["Smtp:Host"];
            var portString = _configuration["Smtp:Port"];
            var user = _configuration["Smtp:User"];
            var pass = _configuration["Smtp:Password"];
            var from = _configuration["Smtp:From"] ?? "no-reply@Raven.ma";
            var enableSsl = bool.TryParse(_configuration["Smtp:EnableSsl"], out var ssl) && ssl;

            var subject = "Raven — Réinitialisation de votre mot de passe";

            var textBody = $@"Raven — Maintenance Industrielle

Une demande de réinitialisation de mot de passe a été effectuée pour votre compte.

Pour définir un nouveau mot de passe, veuillez ouvrir le lien suivant dans votre navigateur :
{resetLink}

Ce lien est valable pendant 30 minutes.

Si vous n'êtes pas Ã  l'origine de cette demande, vous pouvez ignorer cet e-mail.
";

            var htmlBody = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Raven — Réinitialisation</title>
</head>
<body style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background-color: #f5f5f7; margin: 0; padding: 30px;"">
    <div style=""max-width: 500px; margin: 0 auto; background-color: #ffffff; border: 1px solid #d2d2d7; border-radius: 12px; padding: 32px; box-shadow: 0 4px 12px rgba(0,0,0,0.05);"">
        <div style=""text-align: center; margin-bottom: 24px;"">
            <h2 style=""color: #0d9488; margin: 0 0 6px 0; font-size: 24px;"">Raven</h2>
            <p style=""color: #6e6e73; font-size: 14px; margin: 0;"">Plateforme Maintenance Industrielle</p>
        </div>
        <div style=""border-top: 1px solid #e8e8ed; padding-top: 20px; color: #1d1d1f; font-size: 15px; line-height: 1.5;"">
            <p style=""margin: 0 0 16px 0;"">Bonjour,</p>
            <p style=""margin: 0 0 20px 0;"">Une demande de réinitialisation de mot de passe a été demandée pour votre compte Raven.</p>
            <div style=""text-align: center; margin: 28px 0;"">
                <a href=""{resetLink}"" style=""background-color: #0d9488; color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 8px; font-weight: 600; font-size: 14px; display: inline-block;"">
                    Réinitialiser mon mot de passe
                </a>
            </div>
            <p style=""color: #6e6e73; font-size: 13px; margin: 0 0 10px 0;"">
                Ce lien est valable pendant <strong>30 minutes</strong>.
            </p>
            <p style=""color: #86868b; font-size: 12px; margin: 0;"">
                Si vous n'avez pas demandé cette réinitialisation, veuillez ignorer ce message en toute sécurité.
            </p>
        </div>
    </div>
</body>
</html>";

            if (string.IsNullOrWhiteSpace(host))
            {
                // Mode développement : loguer le lien généré
                _logger.LogInformation(
                    "[EmailService DEV] E-mail de réinitialisation pour {ToEmail} :\nLien de réinitialisation : {ResetLink}",
                    toEmail,
                    resetLink);
                return;
            }

            try
            {
                int port = int.TryParse(portString, out var p) ? p : 587;
                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    Credentials = !string.IsNullOrWhiteSpace(user)
                        ? new NetworkCredential(user, pass)
                        : null
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(from, "Raven"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                var plainTextView = AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain");
                var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, null, "text/html");
                mailMessage.AlternateViews.Add(plainTextView);
                mailMessage.AlternateViews.Add(htmlView);

                await client.SendMailAsync(mailMessage);

                _logger.LogInformation("E-mail de réinitialisation envoyé avec succès Ã  {ToEmail}.", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec lors de l'envoi de l'e-mail de réinitialisation Ã  {ToEmail}.", toEmail);
            }
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var host = _configuration["Smtp:Host"];
            var portString = _configuration["Smtp:Port"];
            var user = _configuration["Smtp:User"];
            var pass = _configuration["Smtp:Password"];
            var from = _configuration["Smtp:From"] ?? "no-reply@Raven.ma";
            var enableSsl = bool.TryParse(_configuration["Smtp:EnableSsl"], out var ssl) && ssl;

            if (string.IsNullOrWhiteSpace(host))
            {
                _logger.LogInformation(
                    "[EmailService DEV] E-mail non envoyé (SMTP non configuré). Sujet : {Subject} â†’ {ToEmail}",
                    subject, toEmail);
                return;
            }

            try
            {
                int port = int.TryParse(portString, out var p) ? p : 587;
                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    Credentials = !string.IsNullOrWhiteSpace(user)
                        ? new NetworkCredential(user, pass)
                        : null
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(from, "Raven"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);
                await client.SendMailAsync(mailMessage);

                _logger.LogInformation("E-mail '{Subject}' envoyé avec succès Ã  {ToEmail}.", subject, toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec de l'envoi de l'e-mail '{Subject}' Ã  {ToEmail}.", subject, toEmail);
                throw; // Let caller handle (AuthController catches and logs gracefully)
            }
        }
    }
}

