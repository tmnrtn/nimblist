using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Nimblist.api.Controllers;
using Nimblist.api.DTO;
using Nimblist.api.Hubs;
using Nimblist.api.Services;
using Nimblist.Data;
using Nimblist.Data.Models;
using Xunit;

namespace Nimblist.test.Controllers
{
    public class MealPlanEntriesControllerTests
    {
        private const string OwnerUserId = "owner-user-id";
        private const string SharedUserId = "shared-user-id";
        private const string OtherUserId = "other-user-id";

        private readonly Mock<IHubContext<ShoppingListHub>> _mockHub;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockProxy;
        private readonly Mock<IClassificationService> _mockClassification;

        public MealPlanEntriesControllerTests()
        {
            _mockHub = new Mock<IHubContext<ShoppingListHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockProxy = new Mock<IClientProxy>();
            _mockHub.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockProxy.Object);

            _mockClassification = new Mock<IClassificationService>();
            _mockClassification
                .Setup(s => s.ClassifyAsync(It.IsAny<string>()))
                .ReturnsAsync(((Guid?)null, (Guid?)null));
        }

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

        private MealPlanEntriesController CreateController(NimblistContext context, string userId)
        {
            var controller = new MealPlanEntriesController(
                context, _mockClassification.Object, _mockHub.Object);
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

        private MealPlanEntriesController CreateControllerNoAuth(NimblistContext context)
        {
            var controller = new MealPlanEntriesController(
                context, _mockClassification.Object, _mockHub.Object);
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

        // --- CreateEntry ---

        [Fact]
        public async Task CreateEntry_Owner_ReturnsCreatedWithDto()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var recipeId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            var recipe = new Recipe { Id = recipeId, Title = "Pasta", UserId = OwnerUserId };
            await SeedAsync(opts, plan, recipe);

            var request = new CreateMealPlanEntryRequest(planId, recipeId, new DateOnly(2026, 5, 7), "Dinner", null);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).CreateEntry(request);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<MealPlanEntryDto>(created.Value);
            Assert.Equal(planId, dto.MealPlanId);
            Assert.Equal(recipeId, dto.RecipeId);
            Assert.Equal("Pasta", dto.RecipeTitle);
            Assert.Equal("Dinner", dto.MealType);
            Assert.Equal(new DateOnly(2026, 5, 7), dto.PlannedDate);
        }

        [Fact]
        public async Task CreateEntry_SharedUser_ReturnsCreated()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var recipeId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            var recipe = new Recipe { Id = recipeId, Title = "Soup", UserId = OwnerUserId };
            var share = new MealPlanShare { Id = Guid.NewGuid(), MealPlanId = planId, UserId = SharedUserId, SharedAt = DateTimeOffset.UtcNow };
            await SeedAsync(opts, plan, recipe, share);

            var request = new CreateMealPlanEntryRequest(planId, recipeId, new DateOnly(2026, 5, 8), null, null);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, SharedUserId).CreateEntry(request);

            Assert.IsType<CreatedAtActionResult>(result.Result);
        }

        [Fact]
        public async Task CreateEntry_InaccessiblePlan_ReturnsNotFound()
        {
            var opts = NewDb();
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Pizza", UserId = OwnerUserId };
            await SeedAsync(opts, recipe);

            var request = new CreateMealPlanEntryRequest(Guid.NewGuid(), recipeId, new DateOnly(2026, 5, 7), null, null);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).CreateEntry(request);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateEntry_RecipeNotFound_ReturnsNotFound()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            await SeedAsync(opts, plan);

            var request = new CreateMealPlanEntryRequest(planId, Guid.NewGuid(), new DateOnly(2026, 5, 7), null, null);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).CreateEntry(request);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateEntry_NoAuth_ReturnsUnauthorized()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var request = new CreateMealPlanEntryRequest(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 5, 7), null, null);
            var result = await CreateControllerNoAuth(ctx).CreateEntry(request);
            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        // --- DeleteEntry ---

        [Fact]
        public async Task DeleteEntry_Owner_ReturnsNoContentAndDeletesEntry()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var recipeId = Guid.NewGuid();
            var entryId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            var recipe = new Recipe { Id = recipeId, Title = "Pasta", UserId = OwnerUserId };
            var entry = new MealPlanEntry { Id = entryId, MealPlanId = planId, RecipeId = recipeId, PlannedDate = new DateOnly(2026, 5, 7) };
            await SeedAsync(opts, plan, recipe, entry);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).DeleteEntry(entryId);

            Assert.IsType<NoContentResult>(result);
            Assert.Null(await ctx.MealPlanEntries.FindAsync(entryId));
        }

        [Fact]
        public async Task DeleteEntry_EntryNotFound_ReturnsNotFound()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).DeleteEntry(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteEntry_NonOwner_ReturnsForbidAndEntryNotDeleted()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var recipeId = Guid.NewGuid();
            var entryId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            var recipe = new Recipe { Id = recipeId, Title = "Pasta", UserId = OwnerUserId };
            var share = new MealPlanShare { Id = Guid.NewGuid(), MealPlanId = planId, UserId = SharedUserId, SharedAt = DateTimeOffset.UtcNow };
            var entry = new MealPlanEntry { Id = entryId, MealPlanId = planId, RecipeId = recipeId, PlannedDate = new DateOnly(2026, 5, 7) };
            await SeedAsync(opts, plan, recipe, share, entry);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, SharedUserId).DeleteEntry(entryId);

            Assert.IsType<ForbidResult>(result);
            Assert.NotNull(await ctx.MealPlanEntries.FindAsync(entryId));
        }

        [Fact]
        public async Task DeleteEntry_NoAuth_ReturnsUnauthorized()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateControllerNoAuth(ctx).DeleteEntry(Guid.NewGuid());
            Assert.IsType<UnauthorizedResult>(result);
        }

        // --- AddEntryToList ---

        [Fact]
        public async Task AddEntryToList_ValidRequest_AddsItemsAndReturnsCount()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var recipeId = Guid.NewGuid();
            var entryId = Guid.NewGuid();
            var listId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            var recipe = new Recipe { Id = recipeId, Title = "Pasta", UserId = OwnerUserId };
            recipe.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), Text = "flour", ParsedName = "flour", SortOrder = 0 });
            recipe.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), Text = "water", ParsedName = "water", SortOrder = 1 });
            var entry = new MealPlanEntry { Id = entryId, MealPlanId = planId, RecipeId = recipeId, PlannedDate = new DateOnly(2026, 5, 7) };
            var list = new ShoppingList { Id = listId, Name = "List", UserId = OwnerUserId };
            await SeedAsync(opts, plan, recipe, entry, list);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).AddEntryToList(entryId, listId);

            var ok = Assert.IsType<OkObjectResult>(result);
            var addedCount = ok.Value!.GetType().GetProperty("addedCount")!.GetValue(ok.Value);
            Assert.Equal(2, (int)addedCount!);

            var items = await ctx.Items.Where(i => i.ShoppingListId == listId).ToListAsync();
            Assert.Equal(2, items.Count);
        }

        [Fact]
        public async Task AddEntryToList_EntryNotFound_ReturnsNotFound()
        {
            var opts = NewDb();
            var listId = Guid.NewGuid();
            var list = new ShoppingList { Id = listId, Name = "List", UserId = OwnerUserId };
            await SeedAsync(opts, list);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).AddEntryToList(Guid.NewGuid(), listId);
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task AddEntryToList_InaccessiblePlan_ReturnsForbid()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var recipeId = Guid.NewGuid();
            var entryId = Guid.NewGuid();
            var listId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OtherUserId };
            var recipe = new Recipe { Id = recipeId, Title = "Pasta", UserId = OtherUserId };
            var entry = new MealPlanEntry { Id = entryId, MealPlanId = planId, RecipeId = recipeId, PlannedDate = new DateOnly(2026, 5, 7) };
            var list = new ShoppingList { Id = listId, Name = "List", UserId = OwnerUserId };
            await SeedAsync(opts, plan, recipe, entry, list);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).AddEntryToList(entryId, listId);
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task AddEntryToList_ListNotFound_ReturnsNotFound()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var recipeId = Guid.NewGuid();
            var entryId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            var recipe = new Recipe { Id = recipeId, Title = "Pasta", UserId = OwnerUserId };
            var entry = new MealPlanEntry { Id = entryId, MealPlanId = planId, RecipeId = recipeId, PlannedDate = new DateOnly(2026, 5, 7) };
            await SeedAsync(opts, plan, recipe, entry);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).AddEntryToList(entryId, Guid.NewGuid());
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task AddEntryToList_NoIngredients_ReturnsZeroCount()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var recipeId = Guid.NewGuid();
            var entryId = Guid.NewGuid();
            var listId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            var recipe = new Recipe { Id = recipeId, Title = "No-Ingredient Recipe", UserId = OwnerUserId };
            var entry = new MealPlanEntry { Id = entryId, MealPlanId = planId, RecipeId = recipeId, PlannedDate = new DateOnly(2026, 5, 7) };
            var list = new ShoppingList { Id = listId, Name = "List", UserId = OwnerUserId };
            await SeedAsync(opts, plan, recipe, entry, list);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).AddEntryToList(entryId, listId);

            var ok = Assert.IsType<OkObjectResult>(result);
            var addedCount = ok.Value!.GetType().GetProperty("addedCount")!.GetValue(ok.Value);
            Assert.Equal(0, (int)addedCount!);
        }

        [Fact]
        public async Task AddEntryToList_UsesParsedNameWhenAvailable()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var recipeId = Guid.NewGuid();
            var entryId = Guid.NewGuid();
            var listId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            var recipe = new Recipe { Id = recipeId, Title = "Pasta", UserId = OwnerUserId };
            recipe.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), Text = "500g flour", ParsedName = "flour", SortOrder = 0 });
            var entry = new MealPlanEntry { Id = entryId, MealPlanId = planId, RecipeId = recipeId, PlannedDate = new DateOnly(2026, 5, 7) };
            var list = new ShoppingList { Id = listId, Name = "List", UserId = OwnerUserId };
            await SeedAsync(opts, plan, recipe, entry, list);

            using var ctx = new NimblistContext(opts);
            await CreateController(ctx, OwnerUserId).AddEntryToList(entryId, listId);

            var item = await ctx.Items.FirstAsync(i => i.ShoppingListId == listId);
            Assert.Equal("flour", item.Name);
        }

        [Fact]
        public async Task AddEntryToList_FallsBackToTextWhenParsedNameNull()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var recipeId = Guid.NewGuid();
            var entryId = Guid.NewGuid();
            var listId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            var recipe = new Recipe { Id = recipeId, Title = "Pasta", UserId = OwnerUserId };
            recipe.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), Text = "500g flour", ParsedName = null, SortOrder = 0 });
            var entry = new MealPlanEntry { Id = entryId, MealPlanId = planId, RecipeId = recipeId, PlannedDate = new DateOnly(2026, 5, 7) };
            var list = new ShoppingList { Id = listId, Name = "List", UserId = OwnerUserId };
            await SeedAsync(opts, plan, recipe, entry, list);

            using var ctx = new NimblistContext(opts);
            await CreateController(ctx, OwnerUserId).AddEntryToList(entryId, listId);

            var item = await ctx.Items.FirstAsync(i => i.ShoppingListId == listId);
            Assert.Equal("500g flour", item.Name);
        }

        [Fact]
        public async Task AddEntryToList_CallsClassificationForEachIngredient()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var recipeId = Guid.NewGuid();
            var entryId = Guid.NewGuid();
            var listId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            var recipe = new Recipe { Id = recipeId, Title = "Pasta", UserId = OwnerUserId };
            recipe.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), Text = "flour", ParsedName = "flour", SortOrder = 0 });
            recipe.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), Text = "eggs", ParsedName = "eggs", SortOrder = 1 });
            var entry = new MealPlanEntry { Id = entryId, MealPlanId = planId, RecipeId = recipeId, PlannedDate = new DateOnly(2026, 5, 7) };
            var list = new ShoppingList { Id = listId, Name = "List", UserId = OwnerUserId };
            await SeedAsync(opts, plan, recipe, entry, list);

            using var ctx = new NimblistContext(opts);
            await CreateController(ctx, OwnerUserId).AddEntryToList(entryId, listId);

            _mockClassification.Verify(s => s.ClassifyAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task AddEntryToList_SendsSignalRForEachIngredient()
        {
            var opts = NewDb();
            var planId = Guid.NewGuid();
            var recipeId = Guid.NewGuid();
            var entryId = Guid.NewGuid();
            var listId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Plan", UserId = OwnerUserId };
            var recipe = new Recipe { Id = recipeId, Title = "Pasta", UserId = OwnerUserId };
            recipe.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), Text = "flour", ParsedName = "flour", SortOrder = 0 });
            var entry = new MealPlanEntry { Id = entryId, MealPlanId = planId, RecipeId = recipeId, PlannedDate = new DateOnly(2026, 5, 7) };
            var list = new ShoppingList { Id = listId, Name = "List", UserId = OwnerUserId };
            await SeedAsync(opts, plan, recipe, entry, list);

            using var ctx = new NimblistContext(opts);
            await CreateController(ctx, OwnerUserId).AddEntryToList(entryId, listId);

            _mockProxy.Verify(
                p => p.SendCoreAsync("ReceiveItemAdded", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _mockClients.Verify(c => c.Group($"list_{listId}"), Times.Once);
        }

        [Fact]
        public async Task AddEntryToList_NoAuth_ReturnsUnauthorized()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateControllerNoAuth(ctx).AddEntryToList(Guid.NewGuid(), Guid.NewGuid());
            Assert.IsType<UnauthorizedResult>(result);
        }
    }
}
