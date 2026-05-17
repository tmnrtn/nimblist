using Microsoft.EntityFrameworkCore;
using Nimblist.api.DTO;
using Nimblist.Data;

namespace Nimblist.api.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly NimblistContext _context;

        public SubscriptionService(NimblistContext context)
        {
            _context = context;
        }

        public async Task<bool> HasActiveSubscriptionAsync(string userId)
        {
            return await _context.UserSubscriptions.AnyAsync(s =>
                s.UserId == userId &&
                (s.Status == "ACTIVE" || s.Status == "APPROVED"));
        }

        public async Task<SubscriptionStatusDto?> GetSubscriptionStatusAsync(string userId)
        {
            var sub = await _context.UserSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (sub == null)
                return new SubscriptionStatusDto { Tier = "free" };

            var isActive = sub.Status == "ACTIVE" || sub.Status == "APPROVED";
            return new SubscriptionStatusDto
            {
                Tier = isActive ? "paid" : "free",
                Status = sub.Status,
                IsInTrial = sub.IsInTrial,
                TrialEndDate = sub.TrialEndDate,
                NextBillingDate = sub.NextBillingDate,
                PayPalSubscriptionId = sub.PayPalSubscriptionId,
            };
        }
    }
}
