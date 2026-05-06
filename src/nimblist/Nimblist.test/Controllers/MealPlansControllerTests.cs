using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.Controllers;
using Nimblist.api.DTO;
using Nimblist.Data;
using Nimblist.Data.Models;
using System.Collections.Generic;
using Xunit;

namespace Nimblist.test.Controllers
{
    public class MealPlansControllerTests
    {
        private const string OwnerUserId = "owner-user-id";
        private const string SharedUserId = "shared-user-id";
        private const string OtherUserId = "other-user-id";

        private static DbContextOptions<NimblistContext> NewDb()
        {
            var opts = new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            using var ctx = new NimblistContext(opts);
            foreach (var id in new[] { OwnerUserId, SharedUserId, OtherUserId })
                ctx.Users.Add(new ApplicationUser { Id = id, UserName = id, Email = $"{id}@test.com" });
            ctx.SaveChanges();
            return opts;
        }

        private static MealPlansController CreateController(NimblistContext context, string userId)
        {
            var controller = new MealPlansController(context);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mockAuth"))
                }
            };
            return controller;
        }

        private static MealPlansController CreateControllerNoAuth(NimblistContext context)
        {
            var controller = new MealPlansController(context);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "mockAuth"))
                }
            };
            return controller;
        }

        private static async Task SeedAsync(DbContextOptions<NimblistContext> opts, params object[] entities)
        {
            using var ctx = new NimblistContext(opts);
            ctx.AddRange(entities);
            await ctx.SaveChangesAsync();
        }

        // --- GetMealPlans ---

        [Fact]
        public async Task GetMealPlans_ReturnsOwnPlans_WithIsOwnedTrue()
        {
            var opts = NewDb();
            var plan = new MealPlan { Id = Guid.NewGuid(), Name = "My Plan", UserId = OwnerUserId };
            await SeedAsync(opts, plan);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).GetMealPlans();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var plans = Assert.IsAssignableFrom<List<MealPlanSummaryDto>>(ok.Value);
            Assert.Single(plans);
            Assert.Equal(plan.Id, plans[0].Id);
            Assert.True(plans[0].IsOwned);
        }

        [Fact]
        public async Task GetMealPlans_ReturnsUserSharedPlans_WithIsOwnedFalse()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Shared Plan", UserId = OwnerUserId };
            var share = new MealPlanShare { Id = Guid.NewGuid(), MealPlanId = planId, UserId = SharedUserId, SharedAt = DateTimeOffset.UtcNow };
            await SeedAsync(opts, plan, share);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, SharedUserId).GetMealPlans();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var plans = Assert.IsAssignableFrom<List<MealPlanSummaryDto>>(ok.Value);
            Assert.Single(plans);
            Assert.Equal(planId, plans[0].Id);
            Assert.False(plans[0].IsOwned);
        }

        [Fact]
        public async Task GetMealPlans_ReturnsFamilySharedPlans_WithIsOwnedFalse()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var familyId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Family Plan", UserId = OwnerUserId };
            var share = new MealPlanShare { Id = Guid.NewGuid(), MealPlanId = planId, FamilyId = familyId, SharedAt = DateTimeOffset.UtcNow };
            var member = new FamilyMember { Id = Guid.NewGuid(), FamilyId = familyId, UserId = SharedUserId };
            await SeedAsync(opts, plan, share, member);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, SharedUserId).GetMealPlans();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var plans = Assert.IsAssignableFrom<List<MealPlanSummaryDto>>(ok.Value);
            Assert.Single(plans);
            Assert.False(plans[0].IsOwned);
        }

        [Fact]
        public async Task GetMealPlans_DoesNotReturnInaccessiblePlans()
        {
            var opts = NewDb();
            var plan = new MealPlan { Id = Guid.NewGuid(), Name = "Owner Plan", UserId = OwnerUserId };
            await SeedAsync(opts, plan);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OtherUserId).GetMealPlans();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var plans = Assert.IsAssignableFrom<List<MealPlanSummaryDto>>(ok.Value);
            Assert.Empty(plans);
        }

        [Fact]
        public async Task GetMealPlans_NoAuth_ReturnsUnauthorized()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateControllerNoAuth(ctx).GetMealPlans();
            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        // --- CreateMealPlan ---

        [Fact]
        public async Task CreateMealPlan_ValidName_ReturnsCreatedWithDto()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).CreateMealPlan(new CreateMealPlanRequest("Weekly Plan"));

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<MealPlanSummaryDto>(created.Value);
            Assert.Equal("Weekly Plan", dto.Name);
            Assert.Equal(OwnerUserId, dto.OwnerId);
            Assert.True(dto.IsOwned);
        }

        [Fact]
        public async Task CreateMealPlan_TrimsWhitespaceFromName()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).CreateMealPlan(new CreateMealPlanRequest("  My Plan  "));

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<MealPlanSummaryDto>(created.Value);
            Assert.Equal("My Plan", dto.Name);
        }

        [Fact]
        public async Task CreateMealPlan_EmptyName_ReturnsBadRequest()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).CreateMealPlan(new CreateMealPlanRequest(""));
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateMealPlan_WhitespaceName_ReturnsBadRequest()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).CreateMealPlan(new CreateMealPlanRequest("   "));
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateMealPlan_NoAuth_ReturnsUnauthorized()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateControllerNoAuth(ctx).CreateMealPlan(new CreateMealPlanRequest("Plan"));
            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        // --- GetEntries ---

        [Fact]
        public async Task GetEntries_ReturnsEntriesInDateRange()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var recipeId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            var recipe = new Recipe { Id = recipeId, Title = "Pasta", UserId = OwnerUserId };
            var entry = new MealPlanEntry
            {
                Id = Guid.NewGuid(), MealPlanId = planId, RecipeId = recipeId,
                PlannedDate = new DateOnly(2026, 5, 7), MealType = "Dinner"
            };
            await SeedAsync(opts, plan, recipe, entry);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId)
                .GetEntries(planId, new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 11));

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var entries = Assert.IsAssignableFrom<IEnumerable<MealPlanEntryDto>>(ok.Value).ToList();
            Assert.Single(entries);
            Assert.Equal(recipeId, entries[0].RecipeId);
            Assert.Equal("Pasta", entries[0].RecipeTitle);
            Assert.Equal("Dinner", entries[0].MealType);
        }

        [Fact]
        public async Task GetEntries_ExcludesEntriesOutsideDateRange()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var recipeId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            var recipe = new Recipe { Id = recipeId, Title = "Pasta", UserId = OwnerUserId };
            var entry = new MealPlanEntry
            {
                Id = Guid.NewGuid(), MealPlanId = planId, RecipeId = recipeId,
                PlannedDate = new DateOnly(2026, 4, 1)
            };
            await SeedAsync(opts, plan, recipe, entry);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId)
                .GetEntries(planId, new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 11));

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var entries = Assert.IsAssignableFrom<IEnumerable<MealPlanEntryDto>>(ok.Value);
            Assert.Empty(entries);
        }

        [Fact]
        public async Task GetEntries_InaccessiblePlan_ReturnsNotFound()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId)
                .GetEntries(Guid.NewGuid(), new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 11));
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetEntries_SharedPlan_ReturnsEntries()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var recipeId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            var recipe = new Recipe { Id = recipeId, Title = "Soup", UserId = OwnerUserId };
            var share = new MealPlanShare { Id = Guid.NewGuid(), MealPlanId = planId, UserId = SharedUserId, SharedAt = DateTimeOffset.UtcNow };
            var entry = new MealPlanEntry
            {
                Id = Guid.NewGuid(), MealPlanId = planId, RecipeId = recipeId,
                PlannedDate = new DateOnly(2026, 5, 7)
            };
            await SeedAsync(opts, plan, recipe, share, entry);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, SharedUserId)
                .GetEntries(planId, new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 11));

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var entries = Assert.IsAssignableFrom<IEnumerable<MealPlanEntryDto>>(ok.Value);
            Assert.Single(entries);
        }

        [Fact]
        public async Task GetEntries_NoAuth_ReturnsUnauthorized()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateControllerNoAuth(ctx)
                .GetEntries(Guid.NewGuid(), new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 11));
            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        // --- DeleteMealPlan ---

        [Fact]
        public async Task DeleteMealPlan_Owner_ReturnsNoContentAndRemovesPlan()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            await SeedAsync(opts, plan);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).DeleteMealPlan(planId);

            Assert.IsType<NoContentResult>(result);
            Assert.Null(await ctx.MealPlans.FindAsync(planId));
        }

        [Fact]
        public async Task DeleteMealPlan_NotFound_ReturnsNotFound()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).DeleteMealPlan(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteMealPlan_NonOwner_ReturnsNotFoundAndDoesNotDelete()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            await SeedAsync(opts, plan);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OtherUserId).DeleteMealPlan(planId);

            Assert.IsType<NotFoundResult>(result);
            Assert.NotNull(await ctx.MealPlans.FindAsync(planId));
        }

        [Fact]
        public async Task DeleteMealPlan_SharedUser_CannotDelete()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            var share = new MealPlanShare { Id = Guid.NewGuid(), MealPlanId = planId, UserId = SharedUserId, SharedAt = DateTimeOffset.UtcNow };
            await SeedAsync(opts, plan, share);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, SharedUserId).DeleteMealPlan(planId);

            Assert.IsType<NotFoundResult>(result);
            Assert.NotNull(await ctx.MealPlans.FindAsync(planId));
        }

        [Fact]
        public async Task DeleteMealPlan_NoAuth_ReturnsUnauthorized()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateControllerNoAuth(ctx).DeleteMealPlan(Guid.NewGuid());
            Assert.IsType<UnauthorizedResult>(result);
        }
    }
}
