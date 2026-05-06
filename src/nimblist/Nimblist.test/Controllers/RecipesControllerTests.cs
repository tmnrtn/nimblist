using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Nimblist.api.Controllers;
using Nimblist.api.DTO;
using Nimblist.api.Hubs;
using Nimblist.api.Services;
using Nimblist.Data;
using Nimblist.Data.Models;
using Xunit;

namespace Nimblist.test.Controllers
{
    public class RecipesControllerTests
    {
        private const string OwnerUserId = "owner-user-id";
        private const string OtherUserId = "other-user-id";
        private const string SharedUserId = "shared-user-id";

        private readonly Mock<IHubContext<ShoppingListHub>> _mockHub;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockProxy;
        private readonly Mock<IClassificationService> _mockClassification;
        private readonly Mock<ILogger<RecipesController>> _mockLogger;

        public RecipesControllerTests()
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

            _mockLogger = new Mock<ILogger<RecipesController>>();
        }

        private static DbContextOptions<NimblistContext> NewDb()
        {
            var opts = new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            using var ctx = new NimblistContext(opts);
            foreach (var id in new[] { OwnerUserId, OtherUserId, SharedUserId })
                ctx.Users.Add(new ApplicationUser { Id = id, UserName = id, Email = $"{id}@test.com" });
            ctx.SaveChanges();
            return opts;
        }

        private RecipesController CreateController(NimblistContext context, string userId,
            IHttpClientFactory? httpFactory = null, IConfiguration? config = null)
        {
            var factory = httpFactory ?? BuildHttpFactory(HttpStatusCode.ServiceUnavailable, null);
            var cfg = config ?? BuildConfig(scraperUrl: null, parseUrl: null);

            var controller = new RecipesController(
                context, factory, cfg,
                _mockClassification.Object,
                _mockHub.Object,
                _mockLogger.Object);

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

        private RecipesController CreateControllerNoAuth(NimblistContext context)
        {
            var controller = new RecipesController(
                context,
                BuildHttpFactory(HttpStatusCode.ServiceUnavailable, null),
                BuildConfig(scraperUrl: null, parseUrl: null),
                _mockClassification.Object,
                _mockHub.Object,
                _mockLogger.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "mockAuth"))
                }
            };
            return controller;
        }

        private static IConfiguration BuildConfig(string? scraperUrl, string? parseUrl)
        {
            var values = new Dictionary<string, string?>();
            if (scraperUrl != null) values["RecipeScraperService:ScrapeUrl"] = scraperUrl;
            if (parseUrl != null) values["RecipeScraperService:ParseUrl"] = parseUrl;
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        private static IHttpClientFactory BuildHttpFactory(HttpStatusCode statusCode, object? content)
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = content != null
                        ? JsonContent.Create(content)
                        : new StringContent(string.Empty)
                });

            var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://test/") };
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
            return factory.Object;
        }

        private static async Task SeedAsync(DbContextOptions<NimblistContext> opts, params object[] entities)
        {
            using var ctx = new NimblistContext(opts);
            ctx.AddRange(entities);
            await ctx.SaveChangesAsync();
        }

        private static List<RecipeIngredientInputDto> MakeIngredients(params string[] names) =>
            names.Select((n, i) => new RecipeIngredientInputDto(n, n, null, i)).ToList();

        // --- CreateRecipe ---

        [Fact]
        public async Task CreateRecipe_ValidRequest_ReturnsCreatedWithIngredients()
        {
            var opts = NewDb();
            var request = new CreateRecipeRequest(
                "Pasta Bolognese", "Classic Italian", null, null, "4 servings", 45, "Cook pasta.",
                MakeIngredients("pasta", "mince"));

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).CreateRecipe(request);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<RecipeDetailDto>(created.Value);
            Assert.Equal("Pasta Bolognese", dto.Title);
            Assert.Equal(2, dto.Ingredients.Count);
            Assert.True(dto.IsOwned);
        }

        [Fact]
        public async Task CreateRecipe_EmptyTitle_ReturnsBadRequest()
        {
            var opts = NewDb();
            var request = new CreateRecipeRequest("", null, null, null, null, null, null, new List<RecipeIngredientInputDto>());

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).CreateRecipe(request);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateRecipe_NoAuth_ReturnsUnauthorized()
        {
            var opts = NewDb();
            var request = new CreateRecipeRequest("Pasta", null, null, null, null, null, null, new List<RecipeIngredientInputDto>());

            using var ctx = new NimblistContext(opts);
            var result = await CreateControllerNoAuth(ctx).CreateRecipe(request);

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        // --- GetRecipes ---

        [Fact]
        public async Task GetRecipes_ReturnsOwnRecipes_WithIsOwnedTrue()
        {
            var opts = NewDb();
            var recipe = new Recipe { Id = Guid.NewGuid(), Title = "My Recipe", UserId = OwnerUserId };
            await SeedAsync(opts, recipe);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).GetRecipes();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var recipes = Assert.IsAssignableFrom<List<RecipeSummaryDto>>(ok.Value);
            Assert.Single(recipes);
            Assert.True(recipes[0].IsOwned);
        }

        [Fact]
        public async Task GetRecipes_ReturnsUserSharedRecipes_WithIsOwnedFalse()
        {
            var opts = NewDb();
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Shared Recipe", UserId = OwnerUserId };
            var share = new RecipeShare { Id = Guid.NewGuid(), RecipeId = recipeId, UserId = SharedUserId, SharedAt = DateTimeOffset.UtcNow };
            await SeedAsync(opts, recipe, share);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, SharedUserId).GetRecipes();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var recipes = Assert.IsAssignableFrom<List<RecipeSummaryDto>>(ok.Value);
            Assert.Single(recipes);
            Assert.False(recipes[0].IsOwned);
        }

        [Fact]
        public async Task GetRecipes_ReturnsFamilySharedRecipes()
        {
            var opts = NewDb();
            var recipeId = Guid.NewGuid();
            var familyId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Family Recipe", UserId = OwnerUserId };
            var share = new RecipeShare { Id = Guid.NewGuid(), RecipeId = recipeId, FamilyId = familyId, SharedAt = DateTimeOffset.UtcNow };
            var member = new FamilyMember { Id = Guid.NewGuid(), FamilyId = familyId, UserId = SharedUserId };
            await SeedAsync(opts, recipe, share, member);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, SharedUserId).GetRecipes();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var recipes = Assert.IsAssignableFrom<List<RecipeSummaryDto>>(ok.Value);
            Assert.Single(recipes);
            Assert.False(recipes[0].IsOwned);
        }

        [Fact]
        public async Task GetRecipes_DoesNotReturnInaccessibleRecipes()
        {
            var opts = NewDb();
            var recipe = new Recipe { Id = Guid.NewGuid(), Title = "Private", UserId = OwnerUserId };
            await SeedAsync(opts, recipe);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OtherUserId).GetRecipes();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var recipes = Assert.IsAssignableFrom<List<RecipeSummaryDto>>(ok.Value);
            Assert.Empty(recipes);
        }

        [Fact]
        public async Task GetRecipes_NoAuth_ReturnsUnauthorized()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateControllerNoAuth(ctx).GetRecipes();
            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        // --- GetRecipe ---

        [Fact]
        public async Task GetRecipe_OwnRecipe_ReturnsDetailWithIngredients()
        {
            var opts = NewDb();
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Soup", UserId = OwnerUserId };
            recipe.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), Text = "onion", SortOrder = 0 });
            await SeedAsync(opts, recipe);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).GetRecipe(recipeId);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<RecipeDetailDto>(ok.Value);
            Assert.Equal("Soup", dto.Title);
            Assert.Single(dto.Ingredients);
            Assert.True(dto.IsOwned);
        }

        [Fact]
        public async Task GetRecipe_SharedRecipe_ReturnsWithIsOwnedFalse()
        {
            var opts = NewDb();
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Shared", UserId = OwnerUserId };
            var share = new RecipeShare { Id = Guid.NewGuid(), RecipeId = recipeId, UserId = SharedUserId, SharedAt = DateTimeOffset.UtcNow };
            await SeedAsync(opts, recipe, share);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, SharedUserId).GetRecipe(recipeId);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<RecipeDetailDto>(ok.Value);
            Assert.False(dto.IsOwned);
        }

        [Fact]
        public async Task GetRecipe_InaccessibleId_ReturnsNotFound()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).GetRecipe(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetRecipe_NoAuth_ReturnsUnauthorized()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateControllerNoAuth(ctx).GetRecipe(Guid.NewGuid());
            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        // --- UpdateRecipe ---

        [Fact]
        public async Task UpdateRecipe_Owner_ReturnsOkWithUpdatedTitle()
        {
            var opts = NewDb();
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Old Title", UserId = OwnerUserId };
            await SeedAsync(opts, recipe);

            var request = new CreateRecipeRequest("New Title", null, null, null, null, null, null,
                MakeIngredients("flour"));

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).UpdateRecipe(recipeId, request);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<RecipeDetailDto>(ok.Value);
            Assert.Equal("New Title", dto.Title);
        }

        [Fact]
        public async Task UpdateRecipe_ReplacesIngredients()
        {
            var opts = NewDb();
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Recipe", UserId = OwnerUserId };
            recipe.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), Text = "old ingredient", SortOrder = 0 });
            await SeedAsync(opts, recipe);

            var request = new CreateRecipeRequest("Recipe", null, null, null, null, null, null,
                MakeIngredients("new ingredient 1", "new ingredient 2"));

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).UpdateRecipe(recipeId, request);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<RecipeDetailDto>(ok.Value);
            Assert.Equal(2, dto.Ingredients.Count);
            Assert.DoesNotContain(dto.Ingredients, i => i.Text == "old ingredient");
        }

        [Fact]
        public async Task UpdateRecipe_EmptyTitle_ReturnsBadRequest()
        {
            var opts = NewDb();
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Recipe", UserId = OwnerUserId };
            await SeedAsync(opts, recipe);

            var request = new CreateRecipeRequest("", null, null, null, null, null, null, new List<RecipeIngredientInputDto>());

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).UpdateRecipe(recipeId, request);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task UpdateRecipe_NonOwner_ReturnsNotFound()
        {
            var opts = NewDb();
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Recipe", UserId = OwnerUserId };
            await SeedAsync(opts, recipe);

            var request = new CreateRecipeRequest("New", null, null, null, null, null, null, new List<RecipeIngredientInputDto>());

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OtherUserId).UpdateRecipe(recipeId, request);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task UpdateRecipe_NoAuth_ReturnsUnauthorized()
        {
            var opts = NewDb();
            var request = new CreateRecipeRequest("New", null, null, null, null, null, null, new List<RecipeIngredientInputDto>());

            using var ctx = new NimblistContext(opts);
            var result = await CreateControllerNoAuth(ctx).UpdateRecipe(Guid.NewGuid(), request);

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        // --- DeleteRecipe ---

        [Fact]
        public async Task DeleteRecipe_Owner_ReturnsNoContentAndRemovesRecipe()
        {
            var opts = NewDb();
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Recipe", UserId = OwnerUserId };
            await SeedAsync(opts, recipe);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).DeleteRecipe(recipeId);

            Assert.IsType<NoContentResult>(result);
            Assert.Null(await ctx.Recipes.FindAsync(recipeId));
        }

        [Fact]
        public async Task DeleteRecipe_NotFound_ReturnsNotFound()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).DeleteRecipe(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteRecipe_NonOwner_ReturnsNotFoundAndDoesNotDelete()
        {
            var opts = NewDb();
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Recipe", UserId = OwnerUserId };
            await SeedAsync(opts, recipe);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OtherUserId).DeleteRecipe(recipeId);

            Assert.IsType<NotFoundResult>(result);
            Assert.NotNull(await ctx.Recipes.FindAsync(recipeId));
        }

        [Fact]
        public async Task DeleteRecipe_NoAuth_ReturnsUnauthorized()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateControllerNoAuth(ctx).DeleteRecipe(Guid.NewGuid());
            Assert.IsType<UnauthorizedResult>(result);
        }

        // --- ImportRecipe ---

        [Fact]
        public async Task ImportRecipe_ScraperNotConfigured_Returns503()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).ImportRecipe(new RecipesController.ImportRecipeRequest("https://example.com/recipe"));

            var status = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(503, status.StatusCode);
        }

        [Fact]
        public async Task ImportRecipe_EmptyUrl_ReturnsBadRequest()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).ImportRecipe(new RecipesController.ImportRecipeRequest(""));

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task ImportRecipe_ScraperReturnsError_ReturnsUnprocessableEntity()
        {
            var opts = NewDb();
            var scraperResponse = new ScraperResponseDto { Error = "Could not find recipe data on this page" };
            var factory = BuildHttpFactory(HttpStatusCode.UnprocessableEntity, scraperResponse);
            var config = BuildConfig("http://scraper/scrape", null);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId, factory, config)
                .ImportRecipe(new RecipesController.ImportRecipeRequest("https://example.com/recipe"));

            Assert.IsType<UnprocessableEntityObjectResult>(result.Result);
        }

        [Fact]
        public async Task ImportRecipe_ScraperSucceeds_CreatesAndReturnsRecipe()
        {
            var opts = NewDb();
            var scraperResponse = new ScraperResponseDto
            {
                Title = "Imported Recipe",
                Instructions = "Cook it.",
                Ingredients = new List<ScraperIngredientDto>
                {
                    new ScraperIngredientDto { Text = "flour", ParsedName = "flour" }
                }
            };
            var factory = BuildHttpFactory(HttpStatusCode.OK, scraperResponse);
            var config = BuildConfig("http://scraper/scrape", null);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId, factory, config)
                .ImportRecipe(new RecipesController.ImportRecipeRequest("https://example.com/recipe"));

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<RecipeDetailDto>(created.Value);
            Assert.Equal("Imported Recipe", dto.Title);
            Assert.Single(dto.Ingredients);
        }

        [Fact]
        public async Task ImportRecipe_NoAuth_ReturnsUnauthorized()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateControllerNoAuth(ctx)
                .ImportRecipe(new RecipesController.ImportRecipeRequest("https://example.com/recipe"));
            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        // --- AddIngredientsToList ---

        [Fact]
        public async Task AddIngredientsToList_ValidRecipeAndList_ReturnsOkWithCount()
        {
            var opts = NewDb();
            var recipeId = Guid.NewGuid();
            var listId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Recipe", UserId = OwnerUserId };
            recipe.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), Text = "flour", ParsedName = "flour", SortOrder = 0 });
            recipe.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), Text = "sugar", ParsedName = "sugar", SortOrder = 1 });
            var list = new ShoppingList { Id = listId, Name = "My List", UserId = OwnerUserId };
            await SeedAsync(opts, recipe, list);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).AddIngredientsToList(recipeId, listId);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        [Fact]
        public async Task AddIngredientsToList_UsesParsedNameWhenAvailable()
        {
            var opts = NewDb();
            var recipeId = Guid.NewGuid();
            var listId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Recipe", UserId = OwnerUserId };
            recipe.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), Text = "2 cups of flour", ParsedName = "flour", SortOrder = 0 });
            var list = new ShoppingList { Id = listId, Name = "My List", UserId = OwnerUserId };
            await SeedAsync(opts, recipe, list);

            _mockClassification.Setup(s => s.ClassifyAsync("flour")).ReturnsAsync(((Guid?)null, (Guid?)null));

            using var ctx = new NimblistContext(opts);
            await CreateController(ctx, OwnerUserId).AddIngredientsToList(recipeId, listId);

            _mockClassification.Verify(s => s.ClassifyAsync("flour"), Times.Once);
        }

        [Fact]
        public async Task AddIngredientsToList_FallsBackToTextWhenParsedNameNull()
        {
            var opts = NewDb();
            var recipeId = Guid.NewGuid();
            var listId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Recipe", UserId = OwnerUserId };
            recipe.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), Text = "whole milk", ParsedName = null, SortOrder = 0 });
            var list = new ShoppingList { Id = listId, Name = "My List", UserId = OwnerUserId };
            await SeedAsync(opts, recipe, list);

            using var ctx = new NimblistContext(opts);
            await CreateController(ctx, OwnerUserId).AddIngredientsToList(recipeId, listId);

            _mockClassification.Verify(s => s.ClassifyAsync("whole milk"), Times.Once);
        }

        [Fact]
        public async Task AddIngredientsToList_InaccessibleRecipe_ReturnsNotFound()
        {
            var opts = NewDb();
            var listId = Guid.NewGuid();
            var list = new ShoppingList { Id = listId, Name = "List", UserId = OwnerUserId };
            await SeedAsync(opts, list);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).AddIngredientsToList(Guid.NewGuid(), listId);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task AddIngredientsToList_ListNotOwned_ReturnsNotFound()
        {
            var opts = NewDb();
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Recipe", UserId = OwnerUserId };
            await SeedAsync(opts, recipe);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, OwnerUserId).AddIngredientsToList(recipeId, Guid.NewGuid());

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task AddIngredientsToList_NoAuth_ReturnsUnauthorized()
        {
            var opts = NewDb();
            using var ctx = new NimblistContext(opts);
            var result = await CreateControllerNoAuth(ctx).AddIngredientsToList(Guid.NewGuid(), Guid.NewGuid());
            Assert.IsType<UnauthorizedResult>(result);
        }
    }
}
