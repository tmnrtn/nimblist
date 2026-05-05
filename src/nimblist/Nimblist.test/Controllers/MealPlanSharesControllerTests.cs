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
using Xunit;

namespace Nimblist.test.Controllers
{
    public class MealPlanSharesControllerTests
    {
        private readonly DbContextOptions<NimblistContext> _dbOptions;

        private const string PlanOwnerUserId = "plan-owner-user-id";
        private const string UserToShareWithId = "user-to-share-with-id";
        private const string AnotherUserId = "another-user-id";

        public MealPlanSharesControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            SeedInitialUsers();
        }

        private void SeedInitialUsers()
        {
            using var context = new NimblistContext(_dbOptions);
            foreach (var userId in new[] { PlanOwnerUserId, UserToShareWithId, AnotherUserId })
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

        private MealPlanSharesController CreateController(NimblistContext context, string userId)
        {
            var controller = new MealPlanSharesController(context);
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

        private MealPlanSharesController CreateControllerNoAuth(NimblistContext context)
        {
            var controller = new MealPlanSharesController(context);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "mockAuth"))
                }
            };
            return controller;
        }

        // --- PostShare ---

        [Fact]
        public async Task PostShare_WithValidUser_ReturnsCreatedAtAction()
        {
            var planId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Test Plan", UserId = PlanOwnerUserId };
            await SeedDataAsync(plan);

            var dto = new MealPlanShareInputDto { MealPlanId = planId, UserIdToShareWith = UserToShareWithId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, PlanOwnerUserId).PostShare(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var share = Assert.IsType<MealPlanShareDetailDto>(created.Value);
            Assert.Equal(planId, share.MealPlanId);
            Assert.Equal(UserToShareWithId, share.SharedWithUserId);
            Assert.Null(share.SharedWithFamilyId);
        }

        [Fact]
        public async Task PostShare_WithValidFamily_ReturnsCreatedAtAction()
        {
            var planId = Guid.NewGuid();
            var familyId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Test Plan", UserId = PlanOwnerUserId };
            var family = new Family { Id = familyId, Name = "Test Family", UserId = AnotherUserId };
            await SeedDataAsync(plan, family);

            var dto = new MealPlanShareInputDto { MealPlanId = planId, FamilyIdToShareWith = familyId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, PlanOwnerUserId).PostShare(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var share = Assert.IsType<MealPlanShareDetailDto>(created.Value);
            Assert.Equal(familyId, share.SharedWithFamilyId);
            Assert.Null(share.SharedWithUserId);
        }

        [Fact]
        public async Task PostShare_NeitherUserNorFamily_ReturnsBadRequest()
        {
            var planId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Test Plan", UserId = PlanOwnerUserId };
            await SeedDataAsync(plan);

            var dto = new MealPlanShareInputDto { MealPlanId = planId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, PlanOwnerUserId).PostShare(dto);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostShare_BothUserAndFamily_ReturnsBadRequest()
        {
            var planId = Guid.NewGuid();
            var familyId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Test Plan", UserId = PlanOwnerUserId };
            var family = new Family { Id = familyId, Name = "Test Family", UserId = AnotherUserId };
            await SeedDataAsync(plan, family);

            var dto = new MealPlanShareInputDto { MealPlanId = planId, UserIdToShareWith = UserToShareWithId, FamilyIdToShareWith = familyId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, PlanOwnerUserId).PostShare(dto);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostShare_MealPlanNotFound_ReturnsNotFound()
        {
            var dto = new MealPlanShareInputDto { MealPlanId = Guid.NewGuid(), UserIdToShareWith = UserToShareWithId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, PlanOwnerUserId).PostShare(dto);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostShare_NotOwner_ReturnsForbid()
        {
            var planId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Test Plan", UserId = PlanOwnerUserId };
            await SeedDataAsync(plan);

            var dto = new MealPlanShareInputDto { MealPlanId = planId, UserIdToShareWith = UserToShareWithId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, AnotherUserId).PostShare(dto);

            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task PostShare_SharingWithSelf_ReturnsBadRequest()
        {
            var planId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Test Plan", UserId = PlanOwnerUserId };
            await SeedDataAsync(plan);

            var dto = new MealPlanShareInputDto { MealPlanId = planId, UserIdToShareWith = PlanOwnerUserId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, PlanOwnerUserId).PostShare(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("Cannot share with yourself", bad.Value!.ToString());
        }

        [Fact]
        public async Task PostShare_DuplicateUserShare_ReturnsConflict()
        {
            var planId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Test Plan", UserId = PlanOwnerUserId };
            var existing = new MealPlanShare { Id = Guid.NewGuid(), MealPlanId = planId, UserId = UserToShareWithId, SharedAt = DateTimeOffset.UtcNow };
            await SeedDataAsync(plan, existing);

            var dto = new MealPlanShareInputDto { MealPlanId = planId, UserIdToShareWith = UserToShareWithId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, PlanOwnerUserId).PostShare(dto);

            Assert.IsType<ConflictObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostShare_DuplicateFamilyShare_ReturnsConflict()
        {
            var planId = Guid.NewGuid();
            var familyId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Test Plan", UserId = PlanOwnerUserId };
            var family = new Family { Id = familyId, Name = "Test Family", UserId = AnotherUserId };
            var existing = new MealPlanShare { Id = Guid.NewGuid(), MealPlanId = planId, FamilyId = familyId, SharedAt = DateTimeOffset.UtcNow };
            await SeedDataAsync(plan, family, existing);

            var dto = new MealPlanShareInputDto { MealPlanId = planId, FamilyIdToShareWith = familyId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, PlanOwnerUserId).PostShare(dto);

            Assert.IsType<ConflictObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostShare_UserNotFound_ReturnsBadRequest()
        {
            var planId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Test Plan", UserId = PlanOwnerUserId };
            await SeedDataAsync(plan);

            var dto = new MealPlanShareInputDto { MealPlanId = planId, UserIdToShareWith = "non-existent-user" };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, PlanOwnerUserId).PostShare(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("User not found", bad.Value!.ToString());
        }

        [Fact]
        public async Task PostShare_FamilyNotFound_ReturnsBadRequest()
        {
            var planId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Test Plan", UserId = PlanOwnerUserId };
            await SeedDataAsync(plan);

            var dto = new MealPlanShareInputDto { MealPlanId = planId, FamilyIdToShareWith = Guid.NewGuid() };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, PlanOwnerUserId).PostShare(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("Family not found", bad.Value!.ToString());
        }

        [Fact]
        public async Task PostShare_NoAuth_ReturnsUnauthorized()
        {
            var dto = new MealPlanShareInputDto { MealPlanId = Guid.NewGuid(), UserIdToShareWith = UserToShareWithId };

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateControllerNoAuth(context).PostShare(dto);

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        // --- DeleteShare ---

        [Fact]
        public async Task DeleteShare_OwnerDeletes_ReturnsNoContent()
        {
            var planId = Guid.NewGuid();
            var shareId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Test Plan", UserId = PlanOwnerUserId };
            var share = new MealPlanShare { Id = shareId, MealPlanId = planId, UserId = UserToShareWithId, SharedAt = DateTimeOffset.UtcNow };
            await SeedDataAsync(plan, share);

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, PlanOwnerUserId).DeleteShare(shareId);

            Assert.IsType<NoContentResult>(result);
            Assert.Null(await context.MealPlanShares.FindAsync(shareId));
        }

        [Fact]
        public async Task DeleteShare_ShareNotFound_ReturnsNotFound()
        {
            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, PlanOwnerUserId).DeleteShare(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteShare_NonOwnerDeletes_ReturnsForbid()
        {
            var planId = Guid.NewGuid();
            var shareId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Test Plan", UserId = PlanOwnerUserId };
            var share = new MealPlanShare { Id = shareId, MealPlanId = planId, UserId = UserToShareWithId, SharedAt = DateTimeOffset.UtcNow };
            await SeedDataAsync(plan, share);

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, AnotherUserId).DeleteShare(shareId);

            Assert.IsType<ForbidResult>(result);
            Assert.NotNull(await context.MealPlanShares.FindAsync(shareId));
        }

        [Fact]
        public async Task DeleteShare_NoAuth_ReturnsUnauthorized()
        {
            using var context = new NimblistContext(_dbOptions);
            var result = await CreateControllerNoAuth(context).DeleteShare(Guid.NewGuid());

            Assert.IsType<UnauthorizedResult>(result);
        }

        // --- GetSharesForPlan ---

        [Fact]
        public async Task GetSharesForPlan_Owner_ReturnsAllShares()
        {
            var planId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Test Plan", UserId = PlanOwnerUserId };
            var share1 = new MealPlanShare { Id = Guid.NewGuid(), MealPlanId = planId, UserId = UserToShareWithId, SharedAt = DateTimeOffset.UtcNow };
            var share2 = new MealPlanShare { Id = Guid.NewGuid(), MealPlanId = planId, UserId = AnotherUserId, SharedAt = DateTimeOffset.UtcNow };
            await SeedDataAsync(plan, share1, share2);

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, PlanOwnerUserId).GetSharesForPlan(planId);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var shares = Assert.IsAssignableFrom<IEnumerable<MealPlanShareDetailDto>>(ok.Value);
            Assert.Equal(2, shares.Count());
        }

        [Fact]
        public async Task GetSharesForPlan_NotOwner_ReturnsForbid()
        {
            var planId = Guid.NewGuid();
            var plan = new MealPlan { Id = planId, Name = "Test Plan", UserId = PlanOwnerUserId };
            await SeedDataAsync(plan);

            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, AnotherUserId).GetSharesForPlan(planId);

            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task GetSharesForPlan_PlanNotFound_ReturnsNotFound()
        {
            using var context = new NimblistContext(_dbOptions);
            var result = await CreateController(context, PlanOwnerUserId).GetSharesForPlan(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetSharesForPlan_NoAuth_ReturnsUnauthorized()
        {
            using var context = new NimblistContext(_dbOptions);
            var result = await CreateControllerNoAuth(context).GetSharesForPlan(Guid.NewGuid());

            Assert.IsType<UnauthorizedResult>(result.Result);
        }
    }
}
