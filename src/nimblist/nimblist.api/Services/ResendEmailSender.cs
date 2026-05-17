using Microsoft.AspNetCore.Identity.UI.Services;
using Resend;

namespace Nimblist.api.Services;

public class ResendEmailSender : IEmailSender
{
    private readonly IResend _resend;
    private readonly string _fromAddress;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(IResend resend, IConfiguration configuration, ILogger<ResendEmailSender> logger)
    {
        _resend = resend;
        _fromAddress = configuration["Resend:FromAddress"] ?? throw new InvalidOperationException("Resend:FromAddress is not configured.");
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var message = new EmailMessage
        {
            From = _fromAddress,
            Subject = subject,
            HtmlBody = htmlMessage,
        };
        message.To.Add(email);

        try
        {
            await _resend.EmailSendAsync(message);
            _logger.LogInformation("Email sent to {Email} with subject {Subject}", email, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} with subject {Subject}", email, subject);
            throw;
        }
    }
}
