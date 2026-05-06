using System;
using System.Collections.Generic;
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
using static Nimblist.api.Controllers.ClassificationFeedbackController;

namespace Nimblist.test.Controllers
{
    public class ClassificationFeedbackControllerTests
    {
        private readonly DbContextOptions<NimblistContext> _dbOptions;
        private const string UserId = "test-user-id";

        public ClassificationFeedbackControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        private async Task SeedAsync(params object[] entities)
        {
            using var ctx = new NimblistContext(_dbOptions);
            ctx.AddRange(entities);
            await ctx.SaveChangesAsync();
        }

        private ClassificationFeedbackController CreateController(NimblistContext ctx, string? userId = UserId)
        {
            var controller = new ClassificationFeedbackController(ctx);
            var claims = userId != null
                ? new[] { new Claim(ClaimTypes.NameIdentifier, userId) }
                : Array.Empty<Claim>();
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "mockAuth"))
                }
            };
            return controller;
        }

        // --- PostFeedback ---

        [Fact]
        public async Task PostFeedback_NewEntry_CreatesAndReturnsNoContent()
        {
            var categoryId = Guid.NewGuid();
            var request = new FeedbackRequest("Milk", categoryId, null);

            using var ctx = new NimblistContext(_dbOptions);
            var result = await CreateController(ctx).PostFeedback(request);

            Assert.IsType<NoContentResult>(result);
            using var verify = new NimblistContext(_dbOptions);
            var saved = await verify.ClassificationFeedback.SingleAsync(f => f.UserId == UserId && f.ItemName == "Milk");
            Assert.Equal(categoryId, saved.CategoryId);
        }

        [Fact]
        public async Task PostFeedback_ExistingEntry_UpdatesCategoryAndReturnsNoContent()
        {
            var oldCategoryId = Guid.NewGuid();
            var newCategoryId = Guid.NewGuid();
            await SeedAsync(new ItemClassificationFeedback
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                ItemName = "Bread",
                CategoryId = oldCategoryId,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            });

            var request = new FeedbackRequest("Bread", newCategoryId, null);

            using var ctx = new NimblistContext(_dbOptions);
            var result = await CreateController(ctx).PostFeedback(request);

            Assert.IsType<NoContentResult>(result);
            using var verify = new NimblistContext(_dbOptions);
            var entries = await verify.ClassificationFeedback.Where(f => f.UserId == UserId && f.ItemName == "Bread").ToListAsync();
            Assert.Single(entries);
            Assert.Equal(newCategoryId, entries[0].CategoryId);
        }

        [Fact]
        public async Task PostFeedback_EmptyItemName_ReturnsBadRequest()
        {
            var request = new FeedbackRequest("   ", null, null);

            using var ctx = new NimblistContext(_dbOptions);
            var result = await CreateController(ctx).PostFeedback(request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task PostFeedback_NoAuth_ReturnsUnauthorized()
        {
            var request = new FeedbackRequest("Milk", null, null);

            using var ctx = new NimblistContext(_dbOptions);
            var result = await CreateController(ctx, userId: null).PostFeedback(request);

            Assert.IsType<UnauthorizedResult>(result);
        }

        // --- Export ---

        [Fact]
        public async Task Export_ReturnsAllFeedbackRows()
        {
            var categoryId = Guid.NewGuid();
            var subCategoryId = Guid.NewGuid();
            await SeedAsync(
                new Category { Id = categoryId, Name = "Dairy" },
                new SubCategory { Id = subCategoryId, Name = "Milk", ParentCategoryId = categoryId },
                new ItemClassificationFeedback { Id = Guid.NewGuid(), UserId = UserId, ItemName = "Whole Milk", CategoryId = categoryId, SubCategoryId = subCategoryId, CreatedAt = DateTimeOffset.UtcNow },
                new ItemClassificationFeedback { Id = Guid.NewGuid(), UserId = "other-user", ItemName = "Cheddar", CategoryId = categoryId, CreatedAt = DateTimeOffset.UtcNow }
            );

            using var ctx = new NimblistContext(_dbOptions);
            var result = await CreateController(ctx).Export();

            var ok = Assert.IsType<OkObjectResult>(result);
            var rows = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value).ToList();
            Assert.Equal(2, rows.Count);
        }

        [Fact]
        public async Task Export_NullCategoryRow_IncludedWithNullFields()
        {
            await SeedAsync(new ItemClassificationFeedback
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                ItemName = "Unknown Item",
                CategoryId = null,
                SubCategoryId = null,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            using var ctx = new NimblistContext(_dbOptions);
            var result = await CreateController(ctx).Export();

            var ok = Assert.IsType<OkObjectResult>(result);
            var rows = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value).ToList();
            Assert.Single(rows);
        }
    }
}
