using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.DTO;
using Nimblist.Data;
using Nimblist.Data.Models;

namespace Nimblist.api.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly NimblistContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SubscriptionService(NimblistContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<bool> HasActiveSubscriptionAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            if (user.IsComplimentaryAccess) return true;
            if (await _userManager.IsInRoleAsync(user, "Admin")) return true;

            var sub = await _context.UserSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (sub == null) return false;

            if (sub.Status == "ACTIVE" || sub.Status == "APPROVED") return true;

            return sub.IsInTrial && sub.TrialEndDate.HasValue && sub.TrialEndDate.Value > DateTime.UtcNow;
        }

        public async Task<SubscriptionStatusDto?> GetSubscriptionStatusAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return new SubscriptionStatusDto { Tier = "free" };

            if (user.IsComplimentaryAccess)
                return new SubscriptionStatusDto { Tier = "paid" };

            if (await _userManager.IsInRoleAsync(user, "Admin"))
                return new SubscriptionStatusDto { Tier = "paid" };

            var sub = await _context.UserSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (sub == null)
                return new SubscriptionStatusDto { Tier = "free" };

            var isActive = sub.Status == "ACTIVE" || sub.Status == "APPROVED";
            var trialActive = sub.IsInTrial && sub.TrialEndDate.HasValue && sub.TrialEndDate.Value > DateTime.UtcNow;

            return new SubscriptionStatusDto
            {
                Tier = (isActive || trialActive) ? "paid" : "free",
                Status = sub.Status,
                IsInTrial = trialActive,
                TrialEndDate = sub.TrialEndDate,
                NextBillingDate = sub.NextBillingDate,
                PayPalSubscriptionId = sub.PayPalSubscriptionId,
            };
        }
    }
}
