using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
    public class AdminControllerTests
    {
        private const string AdminUserId = "admin-user-id";
        private const string AdminUserEmail = "admin@example.com";
        private const string OtherUserId = "other-user-id";
        private const string OtherUserEmail = "other@example.com";

        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

        public AdminControllerTests()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                store.Object, null, null, null, null, null, null, null, null);
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static DbContextOptions<NimblistContext> CreateDbOptions() =>
            new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        private static async Task SeedAsync(DbContextOptions<NimblistContext> opts, params object[] entities)
        {
            using var ctx = new NimblistContext(opts);
            ctx.AddRange(entities);
            await ctx.SaveChangesAsync();
        }

        private AdminController CreateController(NimblistContext context, string userId = AdminUserId)
        {
            var mockPayPal = new Mock<IPayPalService>();
            var controller = new AdminController(_mockUserManager.Object, context, mockPayPal.Object);
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "mockAuth"))
                }
            };
            return controller;
        }

        // -------------------------------------------------------------------------
        // GetUsers
        // -------------------------------------------------------------------------

        [Fact]
        public async Task GetUsers_ReturnsAllUsersWithRoles()
        {
            // Arrange — seed users into the in-memory context so that the EF DbSet
            // (which implements IAsyncEnumerable) is used for ToListAsync.
            var opts = CreateDbOptions();
            var adminUser = new ApplicationUser { Id = AdminUserId, Email = AdminUserEmail, UserName = "admin" };
            var otherUser = new ApplicationUser { Id = OtherUserId, Email = OtherUserEmail, UserName = "other" };

            using (var seedCtx = new NimblistContext(opts))
            {
                seedCtx.Users.AddRange(adminUser, otherUser);
                await seedCtx.SaveChangesAsync();
            }

            // GetUsers calls _userManager.Users.ToListAsync() — delegate to the EF DbSet
            // by opening a context and returning its Users DbSet (which is async-capable).
            using var ctx = new NimblistContext(opts);
            _mockUserManager.Setup(m => m.Users).Returns(ctx.Users);

            _mockUserManager
                .Setup(m => m.GetRolesAsync(It.Is<ApplicationUser>(u => u.Id == AdminUserId)))
                .ReturnsAsync(new List<string> { "Admin" });
            _mockUserManager
                .Setup(m => m.GetRolesAsync(It.Is<ApplicationUser>(u => u.Id == OtherUserId)))
                .ReturnsAsync(new List<string> { "Standard" });

            var controller = CreateController(ctx);

            // Act
            var result = await controller.GetUsers();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<AdminUserDto>>(ok.Value).ToList();
            Assert.Equal(2, list.Count);
            Assert.Contains(list, u => u.UserId == AdminUserId && u.Roles.Contains("Admin"));
            Assert.Contains(list, u => u.UserId == OtherUserId && u.Roles.Contains("Standard"));
        }

        // -------------------------------------------------------------------------
        // SetUserRole
        // -------------------------------------------------------------------------

        [Fact]
        public async Task SetUserRole_InvalidRole_ReturnsBadRequest()
        {
            var opts = CreateDbOptions();
            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            var result = await controller.SetUserRole(OtherUserId, new SetRoleDto { Role = "SuperAdmin" });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SetUserRole_OwnAccount_ReturnsBadRequest()
        {
            var opts = CreateDbOptions();
            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx, AdminUserId);

            // Trying to change own role
            var result = await controller.SetUserRole(AdminUserId, new SetRoleDto { Role = "Standard" });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SetUserRole_UserNotFound_ReturnsNotFound()
        {
            _mockUserManager
                .Setup(m => m.FindByIdAsync(OtherUserId))
                .ReturnsAsync((ApplicationUser?)null);

            var opts = CreateDbOptions();
            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            var result = await controller.SetUserRole(OtherUserId, new SetRoleDto { Role = "Admin" });

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task SetUserRole_ValidRole_ReturnsNoContent_AndCallsUserManager()
        {
            var targetUser = new ApplicationUser { Id = OtherUserId, Email = OtherUserEmail };

            _mockUserManager
                .Setup(m => m.FindByIdAsync(OtherUserId))
                .ReturnsAsync(targetUser);
            _mockUserManager
                .Setup(m => m.GetRolesAsync(targetUser))
                .ReturnsAsync(new List<string> { "Standard" });
            _mockUserManager
                .Setup(m => m.RemoveFromRolesAsync(targetUser, It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(IdentityResult.Success);
            _mockUserManager
                .Setup(m => m.AddToRoleAsync(targetUser, "Admin"))
                .ReturnsAsync(IdentityResult.Success);

            var opts = CreateDbOptions();
            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            var result = await controller.SetUserRole(OtherUserId, new SetRoleDto { Role = "Admin" });

            Assert.IsType<NoContentResult>(result);
            _mockUserManager.Verify(m => m.RemoveFromRolesAsync(targetUser, It.IsAny<IEnumerable<string>>()), Times.Once);
            _mockUserManager.Verify(m => m.AddToRoleAsync(targetUser, "Admin"), Times.Once);
        }

        // -------------------------------------------------------------------------
        // DeleteUser
        // -------------------------------------------------------------------------

        [Fact]
        public async Task DeleteUser_OwnAccount_ReturnsBadRequest()
        {
            var opts = CreateDbOptions();
            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx, AdminUserId);

            var result = await controller.DeleteUser(AdminUserId);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteUser_UserNotFound_ReturnsNotFound()
        {
            _mockUserManager
                .Setup(m => m.FindByIdAsync(OtherUserId))
                .ReturnsAsync((ApplicationUser?)null);

            var opts = CreateDbOptions();
            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            var result = await controller.DeleteUser(OtherUserId);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteUser_Valid_ReturnsNoContent()
        {
            var targetUser = new ApplicationUser { Id = OtherUserId, Email = OtherUserEmail };

            _mockUserManager
                .Setup(m => m.FindByIdAsync(OtherUserId))
                .ReturnsAsync(targetUser);
            _mockUserManager
                .Setup(m => m.DeleteAsync(targetUser))
                .ReturnsAsync(IdentityResult.Success);

            var opts = CreateDbOptions();
            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            var result = await controller.DeleteUser(OtherUserId);

            Assert.IsType<NoContentResult>(result);
            _mockUserManager.Verify(m => m.DeleteAsync(targetUser), Times.Once);
        }

        // -------------------------------------------------------------------------
        // GetFamilies
        // -------------------------------------------------------------------------

        [Fact]
        public async Task GetFamilies_ReturnsAllFamiliesWithMembers()
        {
            // Arrange
            var opts = CreateDbOptions();
            var ownerUser = new ApplicationUser { Id = AdminUserId, Email = AdminUserEmail, UserName = "admin" };
            var memberUser = new ApplicationUser { Id = OtherUserId, Email = OtherUserEmail, UserName = "other" };

            var familyId = Guid.NewGuid();
            var memberId = Guid.NewGuid();

            var family = new Family { Id = familyId, Name = "Test Family", UserId = AdminUserId };
            var member = new FamilyMember
            {
                Id = memberId,
                FamilyId = familyId,
                UserId = OtherUserId,
                Role = "Member",
                JoinedAt = DateTimeOffset.UtcNow,
            };

            // Seed users and family data into in-memory DB
            using (var seedCtx = new NimblistContext(opts))
            {
                seedCtx.Users.Add(ownerUser);
                seedCtx.Users.Add(memberUser);
                seedCtx.Families.Add(family);
                seedCtx.FamilyMembers.Add(member);
                await seedCtx.SaveChangesAsync();
            }

            // UserManager.FindByIdAsync for the family owner
            _mockUserManager
                .Setup(m => m.FindByIdAsync(AdminUserId))
                .ReturnsAsync(ownerUser);

            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            // Act
            var result = await controller.GetFamilies();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var families = Assert.IsAssignableFrom<IEnumerable<AdminFamilyDto>>(ok.Value).ToList();
            Assert.Single(families);

            var dto = families[0];
            Assert.Equal(familyId, dto.Id);
            Assert.Equal("Test Family", dto.Name);
            Assert.Equal(AdminUserId, dto.OwnerUserId);
            Assert.Equal(AdminUserEmail, dto.OwnerEmail);
            Assert.Single(dto.Members);
            Assert.Equal(memberId, dto.Members[0].MemberId);
            Assert.Equal(OtherUserId, dto.Members[0].UserId);
        }

        // -------------------------------------------------------------------------
        // RemoveFamilyMember
        // -------------------------------------------------------------------------

        [Fact]
        public async Task RemoveFamilyMember_NotFound_ReturnsNotFound()
        {
            var opts = CreateDbOptions();
            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            var result = await controller.RemoveFamilyMember(Guid.NewGuid(), Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task RemoveFamilyMember_Valid_ReturnsNoContent_AndRemovesFromDb()
        {
            var opts = CreateDbOptions();
            var familyId = Guid.NewGuid();
            var memberId = Guid.NewGuid();

            // Seed: need a user for the FK, family, and the member
            var ownerUser = new ApplicationUser { Id = AdminUserId, Email = AdminUserEmail, UserName = "admin" };
            var memberUser = new ApplicationUser { Id = OtherUserId, Email = OtherUserEmail, UserName = "other" };
            var family = new Family { Id = familyId, Name = "Family", UserId = AdminUserId };
            var member = new FamilyMember { Id = memberId, FamilyId = familyId, UserId = OtherUserId };

            using (var seedCtx = new NimblistContext(opts))
            {
                seedCtx.Users.AddRange(ownerUser, memberUser);
                seedCtx.Families.Add(family);
                seedCtx.FamilyMembers.Add(member);
                await seedCtx.SaveChangesAsync();
            }

            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            var result = await controller.RemoveFamilyMember(familyId, memberId);

            Assert.IsType<NoContentResult>(result);

            // Verify removed from DB
            using var verifyCtx = new NimblistContext(opts);
            var removed = await verifyCtx.FamilyMembers.FindAsync(memberId);
            Assert.Null(removed);
        }

        // -------------------------------------------------------------------------
        // DeleteFamily
        // -------------------------------------------------------------------------

        [Fact]
        public async Task DeleteFamily_NotFound_ReturnsNotFound()
        {
            var opts = CreateDbOptions();
            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            var result = await controller.DeleteFamily(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteFamily_Valid_ReturnsNoContent()
        {
            var opts = CreateDbOptions();
            var familyId = Guid.NewGuid();

            var ownerUser = new ApplicationUser { Id = AdminUserId, Email = AdminUserEmail, UserName = "admin" };
            var family = new Family { Id = familyId, Name = "ToDelete", UserId = AdminUserId };

            using (var seedCtx = new NimblistContext(opts))
            {
                seedCtx.Users.Add(ownerUser);
                seedCtx.Families.Add(family);
                await seedCtx.SaveChangesAsync();
            }

            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            var result = await controller.DeleteFamily(familyId);

            Assert.IsType<NoContentResult>(result);

            using var verifyCtx = new NimblistContext(opts);
            Assert.Null(await verifyCtx.Families.FindAsync(familyId));
        }

        // -------------------------------------------------------------------------
        // GetClassificationFeedback
        // -------------------------------------------------------------------------

        [Fact]
        public async Task GetClassificationFeedback_ReturnsOrderedFeedback()
        {
            var opts = CreateDbOptions();
            var categoryId = Guid.NewGuid();
            var subCategoryId = Guid.NewGuid();
            var newerFeedbackId = Guid.NewGuid();
            var olderFeedbackId = Guid.NewGuid();

            var category = new Category { Id = categoryId, Name = "Dairy" };
            var subCategory = new SubCategory { Id = subCategoryId, Name = "Milk", ParentCategoryId = categoryId };
            var newerFeedback = new ItemClassificationFeedback
            {
                Id = newerFeedbackId,
                UserId = AdminUserId,
                ItemName = "Whole Milk",
                CategoryId = categoryId,
                SubCategoryId = subCategoryId,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var olderFeedback = new ItemClassificationFeedback
            {
                Id = olderFeedbackId,
                UserId = OtherUserId,
                ItemName = "Cheddar",
                CategoryId = categoryId,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            };

            await SeedAsync(opts, category, subCategory, newerFeedback, olderFeedback);

            // GetClassificationFeedback calls _userManager.Users.Where(...).ToDictionaryAsync(...)
            // which requires IAsyncEnumerable. Seed users into the same in-memory DB and
            // delegate _userManager.Users to the EF DbSet so async LINQ works.
            using (var userSeedCtx = new NimblistContext(opts))
            {
                userSeedCtx.Users.Add(new ApplicationUser { Id = AdminUserId, Email = AdminUserEmail, UserName = "admin" });
                userSeedCtx.Users.Add(new ApplicationUser { Id = OtherUserId, Email = OtherUserEmail, UserName = "other" });
                await userSeedCtx.SaveChangesAsync();
            }

            using var ctx = new NimblistContext(opts);
            _mockUserManager.Setup(m => m.Users).Returns(ctx.Users);
            var controller = CreateController(ctx);

            var result = await controller.GetClassificationFeedback();

            var ok = Assert.IsType<OkObjectResult>(result);
            var rows = Assert.IsAssignableFrom<IEnumerable<AdminFeedbackDto>>(ok.Value).ToList();
            Assert.Equal(2, rows.Count);

            // First item must be the newer one (ordered descending by CreatedAt)
            Assert.Equal(newerFeedbackId, rows[0].Id);
            Assert.Equal("Whole Milk", rows[0].ItemName);
            Assert.Equal("Dairy", rows[0].CategoryName);
            Assert.Equal("Milk", rows[0].SubCategoryName);
            Assert.Equal(AdminUserEmail, rows[0].UserEmail);

            Assert.Equal(olderFeedbackId, rows[1].Id);
            Assert.Equal(OtherUserEmail, rows[1].UserEmail);
        }

        // -------------------------------------------------------------------------
        // DeleteClassificationFeedback
        // -------------------------------------------------------------------------

        [Fact]
        public async Task DeleteClassificationFeedback_NotFound_ReturnsNotFound()
        {
            var opts = CreateDbOptions();
            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            var result = await controller.DeleteClassificationFeedback(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteClassificationFeedback_Valid_ReturnsNoContent()
        {
            var opts = CreateDbOptions();
            var feedbackId = Guid.NewGuid();
            var feedback = new ItemClassificationFeedback
            {
                Id = feedbackId,
                UserId = AdminUserId,
                ItemName = "Eggs",
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await SeedAsync(opts, feedback);

            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            var result = await controller.DeleteClassificationFeedback(feedbackId);

            Assert.IsType<NoContentResult>(result);

            using var verifyCtx = new NimblistContext(opts);
            Assert.Null(await verifyCtx.ClassificationFeedback.FindAsync(feedbackId));
        }

        // -------------------------------------------------------------------------
        // GetLlmSettings
        // -------------------------------------------------------------------------

        [Fact]
        public async Task GetLlmSettings_NoSettings_ReturnsEmptyDto()
        {
            var opts = CreateDbOptions();
            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            var result = await controller.GetLlmSettings();

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<LlmSettingsDto>(ok.Value);
            Assert.Null(dto.Provider);
            Assert.Null(dto.Model);
            Assert.Null(dto.ApiKey);
        }

        [Fact]
        public async Task GetLlmSettings_WithSettings_ReturnsMaskedApiKey()
        {
            var opts = CreateDbOptions();
            var settings = new LlmSettings
            {
                Id = 1,
                Provider = "openrouter",
                Model = "claude-3-haiku",
                VisionModel = "claude-3-haiku",
                ApiKey = "sk-or-abcd1234efgh5678",
                BaseUrl = null,
                ImageSearchApiKey = "bsa-abcdefgh12345678",
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            await SeedAsync(opts, settings);

            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            var result = await controller.GetLlmSettings();

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<LlmSettingsDto>(ok.Value);
            Assert.Equal("openrouter", dto.Provider);
            Assert.Equal("claude-3-haiku", dto.Model);

            // ApiKey should be masked: first 4 + **** + last 4
            Assert.NotNull(dto.ApiKey);
            Assert.Contains("****", dto.ApiKey);
            Assert.DoesNotContain("abcd1234efgh5678", dto.ApiKey); // raw key not exposed

            // ImageSearchApiKey should also be masked
            Assert.NotNull(dto.ImageSearchApiKey);
            Assert.Contains("****", dto.ImageSearchApiKey);
        }

        // -------------------------------------------------------------------------
        // UpdateLlmSettings
        // -------------------------------------------------------------------------

        [Fact]
        public async Task UpdateLlmSettings_InvalidProvider_ReturnsBadRequest()
        {
            var opts = CreateDbOptions();
            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            var dto = new LlmSettingsDto { Provider = "invalid-provider", Model = "some-model" };
            var result = await controller.UpdateLlmSettings(dto);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UpdateLlmSettings_ValidNewSettings_CreatesAndReturnsDto()
        {
            // No existing LlmSettings row — should create one
            var opts = CreateDbOptions();
            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            var dto = new LlmSettingsDto
            {
                Provider = "OpenRouter", // mixed-case to verify normalisation
                Model = "claude-3-haiku",
                VisionModel = "claude-3-opus",
                ApiKey = "sk-or-newkey1234567890",
                BaseUrl = null,
                ImageSearchApiKey = null,
            };

            var result = await controller.UpdateLlmSettings(dto);

            var ok = Assert.IsType<OkObjectResult>(result);
            var returned = Assert.IsType<LlmSettingsDto>(ok.Value);

            // Provider should be lower-cased
            Assert.Equal("openrouter", returned.Provider);
            Assert.Equal("claude-3-haiku", returned.Model);
            Assert.Equal("claude-3-opus", returned.VisionModel);

            // ApiKey should be masked in the response
            Assert.NotNull(returned.ApiKey);
            Assert.Contains("****", returned.ApiKey);

            // Verify persisted
            using var verifyCtx = new NimblistContext(opts);
            var saved = await verifyCtx.LlmSettings.FirstOrDefaultAsync();
            Assert.NotNull(saved);
            Assert.Equal("openrouter", saved.Provider);
            Assert.Equal("sk-or-newkey1234567890", saved.ApiKey);
        }

        [Fact]
        public async Task UpdateLlmSettings_MaskedKey_DoesNotOverwriteKey()
        {
            // Arrange: seed an existing row with a known API key
            var opts = CreateDbOptions();
            var originalKey = "sk-or-originalkey1234";
            var settings = new LlmSettings
            {
                Id = 1,
                Provider = "openrouter",
                Model = "old-model",
                ApiKey = originalKey,
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            };

            await SeedAsync(opts, settings);

            using var ctx = new NimblistContext(opts);
            var controller = CreateController(ctx);

            // Send a masked key (as if the frontend is echoing back what GET returned)
            var dto = new LlmSettingsDto
            {
                Provider = "openrouter",
                Model = "new-model",
                ApiKey = "sk-o****1234",   // contains "****" — should NOT overwrite
            };

            var result = await controller.UpdateLlmSettings(dto);

            Assert.IsType<OkObjectResult>(result);

            // Verify the original key was preserved in the DB
            using var verifyCtx = new NimblistContext(opts);
            var persisted = await verifyCtx.LlmSettings.FirstOrDefaultAsync();
            Assert.NotNull(persisted);
            Assert.Equal(originalKey, persisted.ApiKey);
            Assert.Equal("new-model", persisted.Model);
        }
    }
}
