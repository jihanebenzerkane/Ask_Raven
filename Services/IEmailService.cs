using System.Threading.Tasks;

namespace Raven.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink);
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
}

