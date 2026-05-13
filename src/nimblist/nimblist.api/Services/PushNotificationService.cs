using Microsoft.EntityFrameworkCore;
using Nimblist.Data;
using Nimblist.Data.Models;
using System.Text.Json;
using WebPush;

namespace Nimblist.api.Services
{
    public interface IPushNotificationService
    {
        Task NotifyItemAddedAsync(Item item, string addedByUserId);
    }

    public class PushNotificationService : IPushNotificationService
    {
        private readonly NimblistContext _context;
        private readonly ILogger<PushNotificationService> _logger;
        private readonly string _vapidPublicKey;
        private readonly string _vapidPrivateKey;
        private readonly string _vapidSubject;

        public PushNotificationService(
            NimblistContext context,
            IConfiguration configuration,
            ILogger<PushNotificationService> logger)
        {
            _context = context;
            _logger = logger;
            _vapidPublicKey = configuration["VapidSettings:PublicKey"] ?? throw new InvalidOperationException("VapidSettings:PublicKey not configured.");
            _vapidPrivateKey = configuration["VapidSettings:PrivateKey"] ?? throw new InvalidOperationException("VapidSettings:PrivateKey not configured.");
            _vapidSubject = configuration["VapidSettings:Subject"] ?? "mailto:noreply@example.com";
        }

        public async Task NotifyItemAddedAsync(Item item, string addedByUserId)
        {
            var list = await _context.ShoppingLists
                .Include(l => l.ListShares)
                    .ThenInclude(s => s.Family)
                        .ThenInclude(f => f!.Members)
                .FirstOrDefaultAsync(l => l.Id == item.ShoppingListId);

            if (list == null) return;

            var userIdsToNotify = new HashSet<string> { list.UserId };
            foreach (var share in list.ListShares)
            {
                if (share.UserId != null) userIdsToNotify.Add(share.UserId);
                if (share.Family != null)
                    foreach (var member in share.Family.Members)
                        userIdsToNotify.Add(member.UserId);
            }
            userIdsToNotify.Remove(addedByUserId);

            if (userIdsToNotify.Count == 0) return;

            var subscriptions = await _context.PushSubscriptions
                .Where(s => userIdsToNotify.Contains(s.UserId))
                .ToListAsync();

            if (subscriptions.Count == 0) return;

            var payload = JsonSerializer.Serialize(new
            {
                title = list.Name,
                body = $"{item.Name} was added to your list",
                url = $"/lists/{list.Id}",
                icon = "/pwa-192x192.png"
            });

            var vapidDetails = new VapidDetails(_vapidSubject, _vapidPublicKey, _vapidPrivateKey);
            var client = new WebPushClient();
            var staleEndpoints = new List<Guid>();

            foreach (var sub in subscriptions)
            {
                try
                {
                    var pushSubscription = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                    await client.SendNotificationAsync(pushSubscription, payload, vapidDetails);
                }
                catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    staleEndpoints.Add(sub.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send push notification to endpoint {Endpoint}", sub.Endpoint);
                }
            }

            if (staleEndpoints.Count > 0)
            {
                var toRemove = await _context.PushSubscriptions
                    .Where(s => staleEndpoints.Contains(s.Id))
                    .ToListAsync();
                _context.PushSubscriptions.RemoveRange(toRemove);
                await _context.SaveChangesAsync();
            }
        }
    }
}
