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

namespace Nimblist.test.Controllers
{
    public class PreviousItemNamesControllerTests
    {
        private readonly DbContextOptions<NimblistContext> _dbOptions;
        private const string UserId = "test-user-id";
        private const string OtherUserId = "other-user-id";

        public PreviousItemNamesControllerTests()
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

        private PreviousItemNamesController CreateController(NimblistContext ctx, string? userId = UserId)
        {
            var controller = new PreviousItemNamesController(ctx);
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

        // --- GetPreviousItemNames ---

        [Fact]
        public async Task GetPreviousItemNames_ReturnsNamesOrderedByLastUsed()
        {
            var now = DateTimeOffset.UtcNow;
            await SeedAsync(
                new PreviousItemName { Id = Guid.NewGuid(), UserId = UserId, Name = "Milk", LastUsed = now.AddHours(-2) },
                new PreviousItemName { Id = Guid.NewGuid(), UserId = UserId, Name = "Bread", LastUsed = now.AddHours(-1) },
                new PreviousItemName { Id = Guid.NewGuid(), UserId = OtherUserId, Name = "Eggs", LastUsed = now }
            );

            using var ctx = new NimblistContext(_dbOptions);
            var result = await CreateController(ctx).GetPreviousItemNames();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var names = Assert.IsAssignableFrom<IEnumerable<string>>(ok.Value).ToList();
            Assert.Equal(2, names.Count);
            Assert.Equal("Bread", names[0]);
            Assert.Equal("Milk", names[1]);
        }

        [Fact]
        public async Task GetPreviousItemNames_NoEntries_ReturnsEmptyList()
        {
            using var ctx = new NimblistContext(_dbOptions);
            var result = await CreateController(ctx).GetPreviousItemNames();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var names = Assert.IsAssignableFrom<IEnumerable<string>>(ok.Value);
            Assert.Empty(names);
        }

        [Fact]
        public async Task GetPreviousItemNames_NoAuth_ReturnsUnauthorized()
        {
            using var ctx = new NimblistContext(_dbOptions);
            var result = await CreateController(ctx, userId: null).GetPreviousItemNames();

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        // --- DeletePreviousItemName ---

        [Fact]
        public async Task DeletePreviousItemName_ExistingName_ReturnsNoContentAndDeletes()
        {
            await SeedAsync(
                new PreviousItemName { Id = Guid.NewGuid(), UserId = UserId, Name = "Milk", LastUsed = DateTimeOffset.UtcNow }
            );

            using var ctx = new NimblistContext(_dbOptions);
            var result = await CreateController(ctx).DeletePreviousItemName("Milk");

            Assert.IsType<NoContentResult>(result);
            using var verify = new NimblistContext(_dbOptions);
            Assert.False(await verify.PreviousItemNames.AnyAsync(p => p.UserId == UserId && p.Name == "Milk"));
        }

        [Fact]
        public async Task DeletePreviousItemName_NameBelongsToOtherUser_ReturnsNotFound()
        {
            await SeedAsync(
                new PreviousItemName { Id = Guid.NewGuid(), UserId = OtherUserId, Name = "Eggs", LastUsed = DateTimeOffset.UtcNow }
            );

            using var ctx = new NimblistContext(_dbOptions);
            var result = await CreateController(ctx).DeletePreviousItemName("Eggs");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeletePreviousItemName_NotFound_ReturnsNotFound()
        {
            using var ctx = new NimblistContext(_dbOptions);
            var result = await CreateController(ctx).DeletePreviousItemName("NonExistent");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeletePreviousItemName_NoAuth_ReturnsUnauthorized()
        {
            using var ctx = new NimblistContext(_dbOptions);
            var result = await CreateController(ctx, userId: null).DeletePreviousItemName("Milk");

            Assert.IsType<UnauthorizedObjectResult>(result);
        }
    }
}
