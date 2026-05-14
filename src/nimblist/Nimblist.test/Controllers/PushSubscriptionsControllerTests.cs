using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.Controllers;
using Nimblist.Data;
using Nimblist.Data.Models;
using Xunit;

namespace Nimblist.test.Controllers
{
    public class PushSubscriptionsControllerTests
    {
        private const string TestUserId = "test-user-id";

        private static DbContextOptions<NimblistContext> NewDb() =>
            new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        private static async Task SeedAsync(DbContextOptions<NimblistContext> opts, params object[] entities)
        {
            using var ctx = new NimblistContext(opts);
            ctx.AddRange(entities);
            await ctx.SaveChangesAsync();
        }

        private static PushSubscriptionsController CreateController(
            NimblistContext context, string? userId)
        {
            var controller = new PushSubscriptionsController(context);

            ClaimsPrincipal user;
            if (userId != null)
            {
                user = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mockAuth"));
            }
            else
            {
                // No NameIdentifier claim — simulates missing/invalid auth
                user = new ClaimsPrincipal(new ClaimsIdentity());
            }

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
            return controller;
        }

        private static PushSubscriptionsController.SubscriptionDto MakeDto(
            string endpoint = "https://push.example.com/sub/abc",
            string p256dh = "p256dh-key",
            string auth = "auth-secret") =>
            new PushSubscriptionsController.SubscriptionDto(
                endpoint,
                new PushSubscriptionsController.SubscriptionKeysDto(p256dh, auth));

        // ── Subscribe ────────────────────────────────────────────────────────

        [Fact]
        public async Task Subscribe_CreatesNewSubscription_WhenEndpointNotExists()
        {
            var opts = NewDb();
            var dto = MakeDto();

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, TestUserId).Subscribe(dto);

            Assert.IsType<OkResult>(result);

            using var verify = new NimblistContext(opts);
            var saved = await verify.PushSubscriptions.SingleAsync();
            Assert.Equal(TestUserId, saved.UserId);
            Assert.Equal(dto.Endpoint, saved.Endpoint);
            Assert.Equal(dto.Keys.P256dh, saved.P256dh);
            Assert.Equal(dto.Keys.Auth, saved.Auth);
        }

        [Fact]
        public async Task Subscribe_UpdatesExistingSubscription_WhenEndpointExists()
        {
            var opts = NewDb();
            var endpoint = "https://push.example.com/sub/existing";

            await SeedAsync(opts, new UserPushSubscription
            {
                UserId = TestUserId,
                Endpoint = endpoint,
                P256dh = "old-p256dh",
                Auth = "old-auth"
            });

            var dto = new PushSubscriptionsController.SubscriptionDto(
                endpoint,
                new PushSubscriptionsController.SubscriptionKeysDto("new-p256dh", "new-auth"));

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, TestUserId).Subscribe(dto);

            Assert.IsType<OkResult>(result);

            // Still only one row
            using var verify = new NimblistContext(opts);
            var all = await verify.PushSubscriptions.ToListAsync();
            Assert.Single(all);
            Assert.Equal("new-p256dh", all[0].P256dh);
            Assert.Equal("new-auth", all[0].Auth);
        }

        [Fact]
        public async Task Subscribe_ReturnsUnauthorized_WhenNoUser()
        {
            var opts = NewDb();
            var dto = MakeDto();

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, userId: null).Subscribe(dto);

            Assert.IsType<UnauthorizedResult>(result);
        }

        // ── Unsubscribe ──────────────────────────────────────────────────────

        [Fact]
        public async Task Unsubscribe_RemovesSubscription_WhenExists()
        {
            var opts = NewDb();
            var endpoint = "https://push.example.com/sub/to-remove";

            await SeedAsync(opts, new UserPushSubscription
            {
                UserId = TestUserId,
                Endpoint = endpoint,
                P256dh = "key",
                Auth = "secret"
            });

            var dto = MakeDto(endpoint: endpoint);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, TestUserId).Unsubscribe(dto);

            Assert.IsType<NoContentResult>(result);

            using var verify = new NimblistContext(opts);
            Assert.Empty(await verify.PushSubscriptions.ToListAsync());
        }

        [Fact]
        public async Task Unsubscribe_ReturnsNoContent_WhenSubscriptionNotFound()
        {
            var opts = NewDb();
            // No subscriptions seeded — should be a no-op, still NoContent
            var dto = MakeDto(endpoint: "https://push.example.com/sub/nonexistent");

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, TestUserId).Unsubscribe(dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Unsubscribe_ReturnsUnauthorized_WhenNoUser()
        {
            var opts = NewDb();
            var dto = MakeDto();

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, userId: null).Unsubscribe(dto);

            Assert.IsType<UnauthorizedResult>(result);
        }
    }
}
