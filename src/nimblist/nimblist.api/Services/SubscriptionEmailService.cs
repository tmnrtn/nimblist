using Microsoft.AspNetCore.Identity.UI.Services;

namespace Nimblist.api.Services;

public interface ISubscriptionEmailService
{
    Task SendWelcomeAsync(string email);
    Task SendSubscriptionActivatedAsync(string email, bool isInTrial, DateTime? trialEndDate);
    Task SendPaymentFailedAsync(string email);
    Task SendSubscriptionCancelledAsync(string email);
    Task SendInviteAsync(string recipientEmail, string senderEmail, string inviteUrl);
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
            <div style="font-family:sans-serif;max-width:600px;margin:auto;color:#111827">
              <div style="background:linear-gradient(135deg,#2563eb,#4f46e5);padding:32px 24px;border-radius:8px 8px 0 0;text-align:center">
                <h1 style="color:#fff;margin:0;font-size:1.75rem">Welcome to Nimblist!</h1>
                <p style="color:#c7d2fe;margin:8px 0 0">Your shopping lists and recipes, perfectly in sync.</p>
              </div>
              <div style="background:#fff;padding:32px 24px;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 8px 8px">
                <p style="margin:0 0 24px">Great to have you on board. Here are three things to try first:</p>

                <table style="width:100%;border-collapse:collapse;margin-bottom:24px">
                  <tr>
                    <td style="width:40px;vertical-align:top;padding:0 12px 20px 0;font-size:1.5rem">🛒</td>
                    <td style="vertical-align:top;padding-bottom:20px">
                      <strong>Create your first shopping list</strong><br>
                      <span style="color:#6b7280;font-size:0.9rem">Head to My Lists and add your first list. You can type items directly and they'll be categorised automatically.</span>
                    </td>
                  </tr>
                  <tr>
                    <td style="width:40px;vertical-align:top;padding:0 12px 20px 0;font-size:1.5rem">👨‍👩‍👧</td>
                    <td style="vertical-align:top;padding-bottom:20px">
                      <strong>Share with your household</strong><br>
                      <span style="color:#6b7280;font-size:0.9rem">Go to Families, create a family, and invite the people you shop for. Your lists update for everyone in real time.</span>
                    </td>
                  </tr>
                  <tr>
                    <td style="width:40px;vertical-align:top;padding:0 12px 0 0;font-size:1.5rem">📖</td>
                    <td style="vertical-align:top">
                      <strong>Import a recipe</strong><br>
                      <span style="color:#6b7280;font-size:0.9rem">On the Recipes page, paste any recipe URL and Nimblist will import it automatically. Add all the ingredients to your shopping list in one tap. Available on the 7-day free trial.</span>
                    </td>
                  </tr>
                </table>

                <p style="margin:0 0 24px;text-align:center">
                  <a href="{_frontendBaseUrl}/lists" style="background:#4f46e5;color:#fff;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:bold;display:inline-block">
                    Go to My Lists
                  </a>
                </p>

                <p style="color:#9ca3af;font-size:0.8rem;margin:0;text-align:center">
                  Questions? Reply to this email or contact <a href="mailto:support@nimblist.co.uk" style="color:#4f46e5">support@nimblist.co.uk</a>.<br>
                  If you didn't create a Nimblist account, you can safely ignore this email.
                </p>
              </div>
            </div>
            """;

        return SendSafe(email, "Welcome to Nimblist — here's how to get started", html);
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

    public Task SendInviteAsync(string recipientEmail, string senderEmail, string inviteUrl)
    {
        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:auto">
              <h2>You've been invited to Nimblist</h2>
              <p><strong>{senderEmail}</strong> has invited you to try Nimblist — a collaborative shopping list and meal planning app.</p>
              <p>Keep your household in sync: shared shopping lists, saved recipes, and a meal planner, all updating in real time.</p>
              <p style="margin:24px 0">
                <a href="{inviteUrl}" style="background:#22c55e;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold">
                  Accept invite
                </a>
              </p>
              <p style="color:#6b7280;font-size:0.875rem">If you weren't expecting this, you can safely ignore it.</p>
            </div>
            """;

        return SendSafe(recipientEmail, $"{senderEmail} has invited you to Nimblist", html);
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
