using Microsoft.AspNetCore.Identity.UI.Services;

namespace Nimblist.api.Services;

public interface ISubscriptionEmailService
{
    Task SendWelcomeAsync(string email);
    Task SendSubscriptionActivatedAsync(string email, bool isInTrial, DateTime? trialEndDate);
    Task SendPaymentFailedAsync(string email);
    Task SendSubscriptionCancelledAsync(string email);
}

public class SubscriptionEmailService : ISubscriptionEmailService
{
    private readonly IEmailSender _emailSender;
    private readonly string _frontendBaseUrl;
    private readonly ILogger<SubscriptionEmailService> _logger;

    public SubscriptionEmailService(
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<SubscriptionEmailService> logger)
    {
        _emailSender = emailSender;
        _frontendBaseUrl = (configuration["FrontendAppSettings:BaseUrl"] ?? "").TrimEnd('/');
        _logger = logger;
    }

    public Task SendWelcomeAsync(string email)
    {
        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:auto">
              <h2>Welcome to Nimblist!</h2>
              <p>We're glad you joined. Start building your shopping lists, saving recipes, and planning meals — all in one place.</p>
              <p style="margin:24px 0">
                <a href="{_frontendBaseUrl}" style="background:#22c55e;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold">
                  Get started
                </a>
              </p>
              <p style="color:#6b7280;font-size:0.875rem">If you didn't create a Nimblist account, you can ignore this email.</p>
            </div>
            """;

        return SendSafe(email, "Welcome to Nimblist!", html);
    }

    public Task SendSubscriptionActivatedAsync(string email, bool isInTrial, DateTime? trialEndDate)
    {
        string detail;
        if (isInTrial && trialEndDate.HasValue)
            detail = $"<p>Your 7-day free trial runs until <strong>{trialEndDate.Value:MMMM d, yyyy}</strong>. You won't be billed until the trial ends.</p>";
        else
            detail = "<p>Your subscription is now active. Thank you for supporting Nimblist!</p>";

        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:auto">
              <h2>Your Nimblist subscription is active</h2>
              {detail}
              <p style="margin:24px 0">
                <a href="{_frontendBaseUrl}" style="background:#22c55e;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold">
                  Go to Nimblist
                </a>
              </p>
            </div>
            """;

        return SendSafe(email, "Your Nimblist subscription is active", html);
    }

    public Task SendPaymentFailedAsync(string email)
    {
        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:auto">
              <h2>Action required: payment failed</h2>
              <p>We weren't able to process your most recent Nimblist payment. Please update your payment method with PayPal to keep enjoying premium features.</p>
              <p style="margin:24px 0">
                <a href="https://www.paypal.com/myaccount/autopay/" style="background:#ef4444;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold">
                  Update payment method
                </a>
              </p>
              <p style="color:#6b7280;font-size:0.875rem">Your access will be suspended until payment is resolved.</p>
            </div>
            """;

        return SendSafe(email, "Action required: payment failed for your Nimblist subscription", html);
    }

    public Task SendSubscriptionCancelledAsync(string email)
    {
        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:auto">
              <h2>Your subscription has been cancelled</h2>
              <p>Your Nimblist subscription has been cancelled. You'll keep access to paid features until the end of your current billing period.</p>
              <p>We're sorry to see you go. If you change your mind, you can resubscribe at any time.</p>
              <p style="margin:24px 0">
                <a href="{_frontendBaseUrl}" style="background:#22c55e;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold">
                  Return to Nimblist
                </a>
              </p>
            </div>
            """;

        return SendSafe(email, "Your Nimblist subscription has been cancelled", html);
    }

    private async Task SendSafe(string email, string subject, string html)
    {
        try
        {
            await _emailSender.SendEmailAsync(email, subject, html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send subscription email '{Subject}' to {Email}", subject, email);
        }
    }
}
