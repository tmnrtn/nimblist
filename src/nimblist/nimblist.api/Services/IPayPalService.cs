using Nimblist.api.DTO;

namespace Nimblist.api.Services
{
    public interface IPayPalService
    {
        Task<PayPalSubscriptionDetails?> GetSubscriptionAsync(string subscriptionId);
        Task<bool> CancelSubscriptionAsync(string subscriptionId, string reason);
        Task<bool> VerifyWebhookSignatureAsync(string transmissionId, string transmissionTime, string certUrl, string authAlgo, string transmissionSig, string webhookId, string rawBody);
        Task<string> CreateProductAndPlanAsync();
    }
}
