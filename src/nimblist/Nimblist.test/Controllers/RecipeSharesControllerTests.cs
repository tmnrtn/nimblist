using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Nimblist.api.Controllers;
using Nimblist.api.DTO;
using Nimblist.api.Services;
using Nimblist.Data;
using Nimblist.Data.Models;
using Xunit;

namespace Nimblist.test.Controllers
{
    public class RecipeSharesControllerTests
    {
        private readonly DbContextOptions<NimblistContext> _dbOptions;

        private const string RecipeOwnerUserId = "recipe-owner-user-id";
        private const string UserToShareWithId = "user-to-share-with-id";
        private const string AnotherUserId = "another-user-id";

        public RecipeSharesControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            SeedInitialUsers();
        }

        private void SeedInitialUsers()
        {
            using var context = new NimblistContext(_dbOptions);
            foreach (var userId in new[] { RecipeOwnerUserId, UserToShareWithId, AnotherUserId })
            {
                if (!context.Users.Any(u => u.Id == userId))
                    context.Users.Add(new ApplicationUser { Id = userId, UserName = $"{userId}-name", Email = $"{userId}@example.com" });
            }
            context.SaveChanges();
        }

        private async Task SeedDataAsync(params object[] entities)
        {
            using var context = new NimblistContext(_dbOptions);
            context.AddRange(entities);
            await context.SaveChangesAsync();
        }

        private RecipeSharesController CreateController(NimblistContext context, string userId)
        {
            var controller = new RecipeSharesController(context, new Mock<IPushNotificationService>().Object);
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            }, "mockAuth"));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
            return controller;
        }

        private RecipeSharesController CreateControllerNoAuth(NimblistContext context)
        {
            var controller = new RecipeSharesController(context, new Mock<IPushNotificationService>().Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "mockAuth"))
                }
            };
            return controller;
        }

        // --- PostRecipeShare ---

        [Fact]
        public async Task PostRecipeShare_WithValidUser_ReturnsCreatedAtAction()
        {
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Test Recipe", UserId = RecipeOwnerUserId };
            await SeedDataAsync(recipe);

            var dto = new RecipeShareInputDto { RecipeId = recipeId, UserIdToShareWith = UserToShareWithId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, RecipeOwnerUserId).PostRecipeShare(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var share = Assert.IsType<RecipeShareDetailDto>(created.Value);
            Assert.Equal(recipeId, share.RecipeId);
            Assert.Equal(UserToShareWithId, share.SharedWithUserId);
            Assert.Null(share.SharedWithFamilyId);
        }

        [Fact]
        public async Task PostRecipeShare_WithValidFamily_ReturnsCreatedAtAction()
        {
            var recipeId = Guid.NewGuid();
            var familyId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Test Recipe", UserId = RecipeOwnerUserId };
            var family = new Family { Id = familyId, Name = "Test Family", UserId = AnotherUserId };
            await SeedDataAsync(recipe, family);

            var dto = new RecipeShareInputDto { RecipeId = recipeId, FamilyIdToShareWith = familyId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, RecipeOwnerUserId).PostRecipeShare(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var share = Assert.IsType<RecipeShareDetailDto>(created.Value);
            Assert.Equal(familyId, share.SharedWithFamilyId);
            Assert.Null(share.SharedWithUserId);
        }

        [Fact]
        public async Task PostRecipeShare_NeitherUserNorFamily_ReturnsBadRequest()
        {
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Test Recipe", UserId = RecipeOwnerUserId };
            await SeedDataAsync(recipe);

            var dto = new RecipeShareInputDto { RecipeId = recipeId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, RecipeOwnerUserId).PostRecipeShare(dto);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostRecipeShare_BothUserAndFamily_ReturnsBadRequest()
        {
            var recipeId = Guid.NewGuid();
            var familyId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Test Recipe", UserId = RecipeOwnerUserId };
            var family = new Family { Id = familyId, Name = "Test Family", UserId = AnotherUserId };
            await SeedDataAsync(recipe, family);

            var dto = new RecipeShareInputDto { RecipeId = recipeId, UserIdToShareWith = UserToShareWithId, FamilyIdToShareWith = familyId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, RecipeOwnerUserId).PostRecipeShare(dto);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostRecipeShare_RecipeNotFound_ReturnsNotFound()
        {
            var dto = new RecipeShareInputDto { RecipeId = Guid.NewGuid(), UserIdToShareWith = UserToShareWithId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, RecipeOwnerUserId).PostRecipeShare(dto);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostRecipeShare_NotOwner_ReturnsForbid()
        {
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Test Recipe", UserId = RecipeOwnerUserId };
            await SeedDataAsync(recipe);

            var dto = new RecipeShareInputDto { RecipeId = recipeId, UserIdToShareWith = UserToShareWithId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, AnotherUserId).PostRecipeShare(dto);

            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task PostRecipeShare_SharingWithSelf_ReturnsBadRequest()
        {
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Test Recipe", UserId = RecipeOwnerUserId };
            await SeedDataAsync(recipe);

            var dto = new RecipeShareInputDto { RecipeId = recipeId, UserIdToShareWith = RecipeOwnerUserId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, RecipeOwnerUserId).PostRecipeShare(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("Cannot share with yourself", bad.Value!.ToString());
        }

        [Fact]
        public async Task PostRecipeShare_DuplicateUserShare_ReturnsConflict()
        {
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Test Recipe", UserId = RecipeOwnerUserId };
            var existing = new RecipeShare { Id = Guid.NewGuid(), RecipeId = recipeId, UserId = UserToShareWithId, SharedAt = DateTimeOffset.UtcNow };
            await SeedDataAsync(recipe, existing);

            var dto = new RecipeShareInputDto { RecipeId = recipeId, UserIdToShareWith = UserToShareWithId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, RecipeOwnerUserId).PostRecipeShare(dto);

            Assert.IsType<ConflictObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostRecipeShare_DuplicateFamilyShare_ReturnsConflict()
        {
            var recipeId = Guid.NewGuid();
            var familyId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Test Recipe", UserId = RecipeOwnerUserId };
            var family = new Family { Id = familyId, Name = "Test Family", UserId = AnotherUserId };
            var existing = new RecipeShare { Id = Guid.NewGuid(), RecipeId = recipeId, FamilyId = familyId, SharedAt = DateTimeOffset.UtcNow };
            await SeedDataAsync(recipe, family, existing);

            var dto = new RecipeShareInputDto { RecipeId = recipeId, FamilyIdToShareWith = familyId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, RecipeOwnerUserId).PostRecipeShare(dto);

            Assert.IsType<ConflictObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostRecipeShare_UserNotFound_ReturnsBadRequest()
        {
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Test Recipe", UserId = RecipeOwnerUserId };
            await SeedDataAsync(recipe);

            var dto = new RecipeShareInputDto { RecipeId = recipeId, UserIdToShareWith = "non-existent-user" };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, RecipeOwnerUserId).PostRecipeShare(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("User not found", bad.Value!.ToString());
        }

        [Fact]
        public async Task PostRecipeShare_FamilyNotFound_ReturnsBadRequest()
        {
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Test Recipe", UserId = RecipeOwnerUserId };
            await SeedDataAsync(recipe);

            var dto = new RecipeShareInputDto { RecipeId = recipeId, FamilyIdToShareWith = Guid.NewGuid() };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, RecipeOwnerUserId).PostRecipeShare(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("Family not found", bad.Value!.ToString());
        }

        [Fact]
        public async Task PostRecipeShare_NoAuth_ReturnsUnauthorized()
        {
            var dto = new RecipeShareInputDto { RecipeId = Guid.NewGuid(), UserIdToShareWith = UserToShareWithId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateControllerNoAuth(context).PostRecipeShare(dto);

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        // --- DeleteRecipeShare ---

        [Fact]
        public async Task DeleteRecipeShare_OwnerDeletes_ReturnsNoContent()
        {
            var recipeId = Guid.NewGuid();
            var shareId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Test Recipe", UserId = RecipeOwnerUserId };
            var share = new RecipeShare { Id = shareId, RecipeId = recipeId, UserId = UserToShareWithId, SharedAt = DateTimeOffset.UtcNow };
            await SeedDataAsync(recipe, share);

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, RecipeOwnerUserId).DeleteRecipeShare(shareId);

            Assert.IsType<NoContentResult>(result);
            Assert.Null(await context.RecipeShares.FindAsync(shareId));
        }

        [Fact]
        public async Task DeleteRecipeShare_ShareNotFound_ReturnsNotFound()
        {
            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, RecipeOwnerUserId).DeleteRecipeShare(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteRecipeShare_NonOwnerDeletes_ReturnsForbid()
        {
            var recipeId = Guid.NewGuid();
            var shareId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Test Recipe", UserId = RecipeOwnerUserId };
            var share = new RecipeShare { Id = shareId, RecipeId = recipeId, UserId = UserToShareWithId, SharedAt = DateTimeOffset.UtcNow };
            await SeedDataAsync(recipe, share);

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, AnotherUserId).DeleteRecipeShare(shareId);

            Assert.IsType<ForbidResult>(result);
            Assert.NotNull(await context.RecipeShares.FindAsync(shareId));
        }

        [Fact]
        public async Task DeleteRecipeShare_NoAuth_ReturnsUnauthorized()
        {
            using var context = new NimblistContext(_dbOptions);
            var result = await CreateControllerNoAuth(context).DeleteRecipeShare(Guid.NewGuid());

            Assert.IsType<UnauthorizedResult>(result);
        }

        // --- GetSharesForRecipe ---

        [Fact]
        public async Task GetSharesForRecipe_Owner_ReturnsAllShares()
        {
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Test Recipe", UserId = RecipeOwnerUserId };
            var share1 = new RecipeShare { Id = Guid.NewGuid(), RecipeId = recipeId, UserId = UserToShareWithId, SharedAt = DateTimeOffset.UtcNow };
            var share2 = new RecipeShare { Id = Guid.NewGuid(), RecipeId = recipeId, UserId = AnotherUserId, SharedAt = DateTimeOffset.UtcNow };
            await SeedDataAsync(recipe, share1, share2);

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, RecipeOwnerUserId).GetSharesForRecipe(recipeId);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var shares = Assert.IsAssignableFrom<IEnumerable<RecipeShareDetailDto>>(ok.Value);
            Assert.Equal(2, shares.Count());
        }

        [Fact]
        public async Task GetSharesForRecipe_NotOwner_ReturnsForbid()
        {
            var recipeId = Guid.NewGuid();
            var recipe = new Recipe { Id = recipeId, Title = "Test Recipe", UserId = RecipeOwnerUserId };
            await SeedDataAsync(recipe);

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, AnotherUserId).GetSharesForRecipe(recipeId);

            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task GetSharesForRecipe_RecipeNotFound_ReturnsNotFound()
        {
            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, RecipeOwnerUserId).GetSharesForRecipe(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetSharesForRecipe_NoAuth_ReturnsUnauthorized()
        {
            using var context = new NimblistContext(_dbOptions);
            var result = await CreateControllerNoAuth(context).GetSharesForRecipe(Guid.NewGuid());

            Assert.IsType<UnauthorizedResult>(result.Result);
        }
    }
}
