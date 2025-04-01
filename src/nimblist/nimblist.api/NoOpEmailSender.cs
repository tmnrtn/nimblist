using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

namespace Nimblist.api.Services // Or choose an appropriate namespace
{
    public class NoOpEmailSender : IEmailSender
    {
        private readonly ILogger<NoOpEmailSender> _logger;

        public NoOpEmailSender(ILogger<NoOpEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Log the email details instead of sending
            _logger.LogWarning("---- DUMMY EMAIL SENDER ----");
            _logger.LogInformation("To: {Email}", email);
            _logger.LogInformation("Subject: {Subject}", subject);
            _logger.LogInformation("Body: {HtmlMessage}", htmlMessage);
            _logger.LogWarning("---- Email not actually sent. ----");

            // Simulate successful sending
            return Task.CompletedTask;
        }
    }
}