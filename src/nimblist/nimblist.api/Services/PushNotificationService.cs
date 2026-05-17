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
        Task NotifyListSharedAsync(string targetUserId, ShoppingList list, string sharedByUserId);
        Task NotifyRecipeSharedAsync(string targetUserId, Recipe recipe, string sharedByUserId);
    }

    public class PushNotificationService : IPushNotificationService
    {
        private readonly NimblistContext _context;
        private readonly ILogger<PushNotificationService> _logger;
        private readonly string? _vapidPublicKey;
        private readonly string? _vapidPrivateKey;
        private readonly string _vapidSubject;

        public PushNotificationService(
            NimblistContext context,
            IConfiguration configuration,
            ILogger<PushNotificationService> logger)
        {
            _context = context;
            _logger = logger;
            _vapidPublicKey = configuration["VapidSettings:PublicKey"];
            _vapidPrivateKey = configuration["VapidSettings:PrivateKey"];
            _vapidSubject = configuration["VapidSettings:Subject"] ?? "mailto:noreply@example.com";
        }

        public Task NotifyItemAddedAsync(Item item, string addedByUserId)
            => NotifyListMembersAsync(item.ShoppingListId, addedByUserId, list => new
            {
                title = list.Name,
                body = $"{item.Name} was added",
                url = $"/lists/{list.Id}",
                icon = "/pwa-192x192.png"
            });

        public async Task NotifyListSharedAsync(string targetUserId, ShoppingList list, string sharedByUserId)
        {
            var payload = new
            {
                title = "List shared with you",
                body = $"\"{list.Name}\" was shared with you",
                url = $"/lists/{list.Id}",
                icon = "/pwa-192x192.png"
            };
            await NotifyUsersAsync(new[] { targetUserId }, sharedByUserId, payload);
        }

        public async Task NotifyRecipeSharedAsync(string targetUserId, Recipe recipe, string sharedByUserId)
        {
            var payload = new
            {
                title = "Recipe shared with you",
                body = $"\"{recipe.Title}\" was shared with you",
                url = $"/recipes/{recipe.Id}",
                icon = "/pwa-192x192.png"
            };
            await NotifyUsersAsync(new[] { targetUserId }, sharedByUserId, payload);
        }

        private async Task NotifyListMembersAsync(Guid listId, string excludeUserId, Func<ShoppingList, object> buildPayload)
        {
            if (!VapidConfigured) return;

            var list = await _context.ShoppingLists
                .Include(l => l.ListShares)
                    .ThenInclude(s => s.Family)
                        .ThenInclude(f => f!.Members)
                .FirstOrDefaultAsync(l => l.Id == listId);

            if (list == null) return;

            var userIds = CollectUserIdsToNotify(list, excludeUserId);
            if (userIds.Count == 0) return;

            await NotifyUsersAsync(userIds, excludeUserId, buildPayload(list));
        }

        private async Task NotifyUsersAsync(IEnumerable<string> targetUserIds, string excludeUserId, object payloadObj)
        {
            if (!VapidConfigured) return;

            var ids = targetUserIds.Where(id => id != excludeUserId).ToHashSet();
            if (ids.Count == 0) return;

            var subscriptions = await _context.PushSubscriptions
                .Where(s => ids.Contains(s.UserId))
                .ToListAsync();

            if (subscriptions.Count == 0) return;

            var payload = JsonSerializer.Serialize(payloadObj);
            var vapidDetails = new VapidDetails(_vapidSubject, _vapidPublicKey!, _vapidPrivateKey!);
            var client = new WebPushClient();
            var staleEndpoints = new List<Guid>();

            foreach (var sub in subscriptions)
            {
                try
                {
                    var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                    await client.SendNotificationAsync(pushSub, payload, vapidDetails);
                }
                catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
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

        private bool VapidConfigured =>
            !string.IsNullOrEmpty(_vapidPublicKey) && !string.IsNullOrEmpty(_vapidPrivateKey);

        private static HashSet<string> CollectUserIdsToNotify(ShoppingList list, string excludeUserId)
        {
            var userIds = new HashSet<string> { list.UserId };
            foreach (var share in list.ListShares)
            {
                if (share.UserId != null) userIds.Add(share.UserId);
                if (share.Family != null)
                    foreach (var member in share.Family.Members)
                        userIds.Add(member.UserId);
            }
            userIds.Remove(excludeUserId);
            return userIds;
        }
    }
}
