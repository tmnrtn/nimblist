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
    public class FamiliesControllerTests
    {
        private readonly DbContextOptions<NimblistContext> _dbOptions;
        private const string TestUserId1 = "test-user-id-1"; // Creator/Owner
        private const string TestUserId2 = "test-user-id-2"; // Another user
        private const string TestUserEmail1 = "user1@example.com";

        public FamiliesControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            SeedInitialUsers();
        }
        private void SeedInitialUsers()
        {
            using (var context = new NimblistContext(_dbOptions))
            {
                // Ensure users exist for FK constraints if ApplicationUser is involved via Include(fm => fm.User)
                if (!context.Users.Any(u => u.Id == TestUserId1))
                {
                    context.Users.Add(new ApplicationUser { Id = TestUserId1, UserName = "user1", Email = TestUserEmail1 });
                }
                if (!context.Users.Any(u => u.Id == TestUserId2))
                {
                    context.Users.Add(new ApplicationUser { Id = TestUserId2, UserName = "user2", Email = "user2@example.com" });
                }
                context.SaveChanges();
            }
        }

        private async Task SeedDataAsync(params object[] entities)
        {
            using (var context = new NimblistContext(_dbOptions))
            {
                context.AddRange(entities);
                await context.SaveChangesAsync();
            }
        }

        private FamiliesController CreateControllerWithContext(NimblistContext context, string userId)
        {
            var controller = new FamiliesController(context);
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            }, "mockAuthenticationType"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
            return controller;
        }

        // --- Tests for GetFamilies ---

        [Fact]
        public async Task GetFamilies_UserHasFamilies_ReturnsOkWithFamiliesAndMembers()
        {
            // Arrange
            var family1 = new Family { Id = Guid.NewGuid(), Name = "Family Alpha", UserId = TestUserId1 };
            var familyMember1 = new FamilyMember { Id = Guid.NewGuid(), FamilyId = family1.Id, UserId = TestUserId1 };
            family1.Members = new List<FamilyMember> { familyMember1 };

            var family2 = new Family { Id = Guid.NewGuid(), Name = "Family Beta", UserId = TestUserId1 };
            var familyMember2 = new FamilyMember { Id = Guid.NewGuid(), FamilyId = family2.Id, UserId = TestUserId1 };
            family2.Members = new List<FamilyMember> { familyMember2 };

            var familyForOtherUser = new Family { Id = Guid.NewGuid(), Name = "Family Gamma", UserId = TestUserId2 };

            await SeedDataAsync(family1, familyMember1, family2, familyMember2, familyForOtherUser);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, TestUserId1);
                // Act
                var result = await controller.GetFamilies();
                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var families = Assert.IsAssignableFrom<IEnumerable<Family>>(okResult.Value).ToList();
                Assert.Equal(2, families.Count);
                Assert.Contains(families, f => f.Name == "Family Alpha" && f.UserId == TestUserId1 && f.Members.Any(m => m.UserId == TestUserId1));
                Assert.Contains(families, f => f.Name == "Family Beta" && f.UserId == TestUserId1 && f.Members.Any(m => m.UserId == TestUserId1));
                Assert.Equal("Family Alpha", families[0].Name);
            }
        }

        [Fact]
        public async Task GetFamilies_UserHasNoFamilies_ReturnsOkWithEmptyList()
        {
            // Arrange
            var familyForOtherUser = new Family { Id = Guid.NewGuid(), Name = "Family Gamma", UserId = TestUserId2 };
            await SeedDataAsync(familyForOtherUser);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, TestUserId1);
                // Act
                var result = await controller.GetFamilies();
                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var families = Assert.IsAssignableFrom<IEnumerable<Family>>(okResult.Value);
                Assert.Empty(families);
            }
        }

        [Fact]
        public async Task GetFamilies_NoUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = new FamiliesController(context);
                var userWithoutNameIdClaim = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { }, "mockAuth_NoNameId"));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = userWithoutNameIdClaim } };

                // Act
                var result = await controller.GetFamilies();
                // Assert
                var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
                Assert.Equal("User ID claim not found.", unauthorizedResult.Value);
            }
        }

        // --- Tests for GetFamily(Guid id) ---

        [Fact]
        public async Task GetFamily_FamilyExistsAndBelongsToUser_ReturnsOkWithFamily()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "My Family", UserId = TestUserId1 };
            var member = new FamilyMember { Id = Guid.NewGuid(), FamilyId = family.Id, UserId = TestUserId1 };
            family.Members = new List<FamilyMember> { member };
            await SeedDataAsync(family, member);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, TestUserId1);
                // Act
                var result = await controller.GetFamily(familyId);
                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var returnedFamily = Assert.IsType<Family>(okResult.Value);
                Assert.Equal(familyId, returnedFamily.Id);
                Assert.Equal(TestUserId1, returnedFamily.UserId);
                Assert.NotNull(returnedFamily.Members);
                Assert.Contains(returnedFamily.Members, m => m.UserId == TestUserId1);
            }
        }

        [Fact]
        public async Task GetFamily_FamilyExistsButBelongsToAnotherUser_ReturnsNotFound()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var familyForOtherUser = new Family { Id = familyId, Name = "Other's Family", UserId = TestUserId2 };
            await SeedDataAsync(familyForOtherUser);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, TestUserId1);
                // Act
                var result = await controller.GetFamily(familyId);
                // Assert
                Assert.IsType<NotFoundResult>(result.Result);
            }
        }

        [Fact]
        public async Task GetFamily_FamilyDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, TestUserId1);
                // Act
                var result = await controller.GetFamily(Guid.NewGuid());
                // Assert
                Assert.IsType<NotFoundResult>(result.Result);
            }
        }

        [Fact]
        public async Task GetFamily_NoUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            await SeedDataAsync(new Family { Id = familyId, Name = "Some Family", UserId = TestUserId1 });
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = new FamiliesController(context);
                var userWithoutNameIdClaim = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { }, "mockAuth_NoNameId"));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = userWithoutNameIdClaim } };

                // Act
                var result = await controller.GetFamily(familyId);
                // Assert
                var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
                Assert.Equal("User ID claim not found.", unauthorizedResult.Value);
            }
        }

        // --- Tests for PostFamily ---

        [Fact]
        public async Task PostFamily_ValidInput_CreatesFamilyAndAddsCreatorAsMember()
        {
            // Arrange
            var familyDto = new FamilyInputDto { Name = "The Creators" };
            Guid createdFamilyId = Guid.Empty;

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, TestUserId1);
                // Act
                var result = await controller.PostFamily(familyDto);
                // Assert (Result)
                var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
                var familyFromResult = Assert.IsType<Family>(createdAtActionResult.Value);
                Assert.Equal(familyDto.Name, familyFromResult.Name);
                Assert.Equal(TestUserId1, familyFromResult.UserId);
                Assert.NotNull(familyFromResult.Members);
                Assert.Single(familyFromResult.Members);
                Assert.Equal(TestUserId1, familyFromResult.Members.First().UserId);
                createdFamilyId = familyFromResult.Id;
            }

            using (var context = new NimblistContext(_dbOptions))
            {
                var dbFamily = await context.Families.FindAsync(createdFamilyId);
                Assert.NotNull(dbFamily);
                Assert.Equal(familyDto.Name, dbFamily.Name);
                Assert.Equal(TestUserId1, dbFamily.UserId);

                var dbMember = await context.FamilyMembers.FirstOrDefaultAsync(fm => fm.FamilyId == createdFamilyId && fm.UserId == TestUserId1);
                Assert.NotNull(dbMember);
            }
        }

        [Fact]
        public async Task PostFamily_NoUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var familyDto = new FamilyInputDto { Name = "Unauthorized Family" };
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = new FamiliesController(context);
                var userWithoutNameIdClaim = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { }, "mockAuth_NoNameId"));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = userWithoutNameIdClaim } };

                // Act
                var result = await controller.PostFamily(familyDto);
                // Assert
                var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
                Assert.Equal("User ID claim not found.", unauthorizedResult.Value);
            }
        }

        // --- Tests for PutFamily ---

        [Fact]
        public async Task PutFamily_UserOwnsFamily_UpdatesName_ReturnsNoContent()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var originalFamily = new Family { Id = familyId, Name = "Old Name", UserId = TestUserId1 };
            await SeedDataAsync(originalFamily);
            var updateDto = new FamilyUpdateDto { Name = "New Name" };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, TestUserId1);
                // Act
                var result = await controller.PutFamily(familyId, updateDto);
                // Assert
                Assert.IsType<NoContentResult>(result);
                var updatedFamily = await context.Families.FindAsync(familyId);
                Assert.Equal("New Name", updatedFamily.Name);
            }
        }

        [Fact]
        public async Task PutFamily_UserDoesNotOwnFamily_ReturnsNotFound()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var familyOfOtherUser = new Family { Id = familyId, Name = "Other's Family", UserId = TestUserId2 };
            await SeedDataAsync(familyOfOtherUser);
            var updateDto = new FamilyUpdateDto { Name = "Attempted Update" };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, TestUserId1);
                // Act
                var result = await controller.PutFamily(familyId, updateDto);
                // Assert
                Assert.IsType<NotFoundResult>(result);
            }
        }

        [Fact]
        public async Task PutFamily_FamilyDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            var updateDto = new FamilyUpdateDto { Name = "Non Existent" };
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, TestUserId1);
                // Act
                var result = await controller.PutFamily(Guid.NewGuid(), updateDto);
                // Assert
                Assert.IsType<NotFoundResult>(result);
            }
        }

        [Fact]
        public async Task PutFamily_NoUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            await SeedDataAsync(new Family { Id = familyId, Name = "Some Family", UserId = TestUserId1 });
            var updateDto = new FamilyUpdateDto { Name = "Updated Name" };
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = new FamiliesController(context);
                var userWithoutNameIdClaim = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { }, "mockAuth_NoNameId"));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = userWithoutNameIdClaim } };

                // Act
                var result = await controller.PutFamily(familyId, updateDto);
                // Assert
                var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
                Assert.Equal("User ID claim not found.", unauthorizedResult.Value);
            }
        }

        // --- Tests for DeleteFamily ---

        [Fact]
        public async Task DeleteFamily_UserOwnsFamily_DeletesFamily_ReturnsNoContent()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var familyToDelete = new Family { Id = familyId, Name = "To Be Deleted", UserId = TestUserId1 };
            var memberInFamily = new FamilyMember { Id = Guid.NewGuid(), FamilyId = familyId, UserId = TestUserId1 };
            await SeedDataAsync(familyToDelete, memberInFamily);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, TestUserId1);
                // Act
                var result = await controller.DeleteFamily(familyId);
                // Assert
                Assert.IsType<NoContentResult>(result);
                Assert.Null(await context.Families.FindAsync(familyId));
            }
        }

        [Fact]
        public async Task DeleteFamily_UserDoesNotOwnFamily_ReturnsNotFound()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var familyOfOtherUser = new Family { Id = familyId, Name = "Other's Family", UserId = TestUserId2 };
            await SeedDataAsync(familyOfOtherUser);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, TestUserId1);
                // Act
                var result = await controller.DeleteFamily(familyId);
                // Assert
                Assert.IsType<NotFoundResult>(result);
                Assert.NotNull(await context.Families.FindAsync(familyId));
            }
        }

        [Fact]
        public async Task DeleteFamily_FamilyDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, TestUserId1);
                // Act
                var result = await controller.DeleteFamily(Guid.NewGuid());
                // Assert
                Assert.IsType<NotFoundResult>(result);
            }
        }

        [Fact]
        public async Task DeleteFamily_NoUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            await SeedDataAsync(new Family { Id = familyId, Name = "Test Family", UserId = "some-other-user" });

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = new FamiliesController(context);
                var userWithoutNameIdClaim = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { }, "mockAuthenticationType_NoNameId"));
                controller.ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = userWithoutNameIdClaim }
                };

                // Act
                var result = await controller.DeleteFamily(familyId);

                // Assert
                var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
                Assert.Equal("User ID claim not found.", unauthorizedResult.Value);
            }
        }
    }
}