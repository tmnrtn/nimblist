using Nimblist.api.DTO;

namespace Nimblist.api.Services
{
    public interface ISubscriptionService
    {
        Task<bool> HasActiveSubscriptionAsync(string userId);
        Task<SubscriptionStatusDto?> GetSubscriptionStatusAsync(string userId);
    }
}
