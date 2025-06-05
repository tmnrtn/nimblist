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
    public class FamilyMembersControllerTests
    {
        private readonly DbContextOptions<NimblistContext> _dbOptions;

        // Define User IDs for different roles in tests
        private const string FamilyOwnerId = "owner-user-id";
        private const string UserToInviteId = "invitee-user-id";
        private const string ExistingMemberId = "existing-member-user-id"; // Could be same as UserToInviteId after being added
        private const string AnotherMemberId = "another-member-user-id";
        private const string UnrelatedUserId = "unrelated-user-id";

        public FamilyMembersControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            SeedInitialUsers(); // Important for FK constraints on FamilyMember.UserId
        }

        private void SeedInitialUsers()
        {
            using (var context = new NimblistContext(_dbOptions))
            {
                var userIds = new[] { FamilyOwnerId, UserToInviteId, ExistingMemberId, AnotherMemberId, UnrelatedUserId };
                foreach (var userId in userIds)
                {
                    if (!context.Users.Any(u => u.Id == userId))
                    {
                        context.Users.Add(new ApplicationUser { Id = userId, UserName = $"{userId}-name", Email = $"{userId}@example.com" });
                    }
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

        private FamilyMembersController CreateControllerWithContext(NimblistContext context, string currentUserId)
        {
            var controller = new FamilyMembersController(context);
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, currentUserId)
            }, "mockAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
            return controller;
        }

        // --- Tests for PostFamilyMember ---

        [Fact]
        public async Task PostFamilyMember_FamilyOwnerAddsUser_ReturnsCreatedAtAction()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "Owner's Family", UserId = FamilyOwnerId }; // FamilyOwnerId owns this family
            await SeedDataAsync(family); // UserToInviteId is already seeded by SeedInitialUsers

            var dto = new FamilyMemberInputDto { FamilyId = familyId, UserIdToAdd = UserToInviteId };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, FamilyOwnerId); // Action performed by FamilyOwner
                // Act
                var result = await controller.PostFamilyMember(dto);
                // Assert
                var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
                var familyMember = Assert.IsType<FamilyMember>(createdAtActionResult.Value);
                Assert.Equal(familyId, familyMember.FamilyId);
                Assert.Equal(UserToInviteId, familyMember.UserId);
                Assert.Equal(nameof(controller.GetFamilyMember), createdAtActionResult.ActionName);

                // Verify in DB
                var dbMember = await context.FamilyMembers.FirstOrDefaultAsync(fm => fm.FamilyId == familyId && fm.UserId == UserToInviteId);
                Assert.NotNull(dbMember);
            }
        }

        [Fact]
        public async Task PostFamilyMember_NonFamilyOwnerTriesToAdd_ReturnsForbid()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "SomeoneElse's Family", UserId = FamilyOwnerId };
            await SeedDataAsync(family);
            var dto = new FamilyMemberInputDto { FamilyId = familyId, UserIdToAdd = UserToInviteId };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, UnrelatedUserId); // UnrelatedUser tries to add
                // Act
                var result = await controller.PostFamilyMember(dto);
                // Assert
                Assert.IsType<ForbidResult>(result.Result); // Forbid specified in controller logic
            }
        }

        [Fact]
        public async Task PostFamilyMember_FamilyNotFound_ReturnsNotFound()
        {
            // Arrange
            var dto = new FamilyMemberInputDto { FamilyId = Guid.NewGuid(), UserIdToAdd = UserToInviteId }; // Non-existent FamilyId
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, FamilyOwnerId);
                // Act
                var result = await controller.PostFamilyMember(dto);
                // Assert
                Assert.IsType<NotFoundObjectResult>(result.Result);
            }
        }

        [Fact]
        public async Task PostFamilyMember_UserToAddNotFound_ReturnsBadRequest()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "Owner's Family", UserId = FamilyOwnerId };
            await SeedDataAsync(family);
            var dto = new FamilyMemberInputDto { FamilyId = familyId, UserIdToAdd = "non-existent-user-id" };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, FamilyOwnerId);
                // Act
                var result = await controller.PostFamilyMember(dto);
                // Assert
                Assert.IsType<BadRequestObjectResult>(result.Result);
            }
        }

        [Fact]
        public async Task PostFamilyMember_UserAlreadyMember_ReturnsConflict()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "Owner's Family", UserId = FamilyOwnerId };
            var existingFamilyMember = new FamilyMember { FamilyId = familyId, UserId = ExistingMemberId };
            await SeedDataAsync(family, existingFamilyMember);
            var dto = new FamilyMemberInputDto { FamilyId = familyId, UserIdToAdd = ExistingMemberId };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, FamilyOwnerId);
                // Act
                var result = await controller.PostFamilyMember(dto);
                // Assert
                Assert.IsType<ConflictObjectResult>(result.Result);
            }
        }

        [Fact]
        public async Task PostFamilyMember_NoCurrentUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var dto = new FamilyMemberInputDto { FamilyId = Guid.NewGuid(), UserIdToAdd = UserToInviteId };
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = new FamilyMembersController(context);
                var userWithoutNameIdClaim = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { }, "mockAuth_NoNameId"));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = userWithoutNameIdClaim } };
                // Act
                var result = await controller.PostFamilyMember(dto);
                // Assert
                Assert.IsType<UnauthorizedObjectResult>(result.Result);
            }
        }

        // --- Tests for DeleteFamilyMember ---

        [Fact]
        public async Task DeleteFamilyMember_FamilyOwnerDeletesMember_ReturnsNoContent()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "Owner's Family", UserId = FamilyOwnerId };
            var memberToDeleteId = Guid.NewGuid();
            var memberToDelete = new FamilyMember { Id = memberToDeleteId, FamilyId = familyId, UserId = ExistingMemberId, Family = family };
            await SeedDataAsync(family, memberToDelete);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, FamilyOwnerId); // Owner deletes
                // Act
                var result = await controller.DeleteFamilyMember(memberToDeleteId);
                // Assert
                Assert.IsType<NoContentResult>(result);
                Assert.Null(await context.FamilyMembers.FindAsync(memberToDeleteId));
            }
        }

        [Fact]
        public async Task DeleteFamilyMember_MemberDeletesSelf_ReturnsNoContent()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "Some Family", UserId = FamilyOwnerId }; // FamilyOwner owns
            var selfMemberId = Guid.NewGuid();
            // ExistingMemberId is deleting themself
            var selfMember = new FamilyMember { Id = selfMemberId, FamilyId = familyId, UserId = ExistingMemberId, Family = family };
            await SeedDataAsync(family, selfMember);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ExistingMemberId); // Member deletes self
                // Act
                var result = await controller.DeleteFamilyMember(selfMemberId);
                // Assert
                Assert.IsType<NoContentResult>(result);
                Assert.Null(await context.FamilyMembers.FindAsync(selfMemberId));
            }
        }

        [Fact]
        public async Task DeleteFamilyMember_UnrelatedUserTriesToDelete_ReturnsForbid()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "Some Family", UserId = FamilyOwnerId };
            var memberId = Guid.NewGuid();
            var member = new FamilyMember { Id = memberId, FamilyId = familyId, UserId = ExistingMemberId, Family = family };
            await SeedDataAsync(family, member);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, UnrelatedUserId); // Unrelated user
                // Act
                var result = await controller.DeleteFamilyMember(memberId);
                // Assert
                Assert.IsType<ForbidResult>(result);
            }
        }

        [Fact]
        public async Task DeleteFamilyMember_NonOwnerMemberTriesToDeleteAnotherMember_ReturnsForbid()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "Team Family", UserId = FamilyOwnerId };
            var member1RecordId = Guid.NewGuid();
            var member1 = new FamilyMember { Id = member1RecordId, FamilyId = familyId, UserId = ExistingMemberId, Family = family };
            var member2RecordId = Guid.NewGuid();
            var member2 = new FamilyMember { Id = member2RecordId, FamilyId = familyId, UserId = AnotherMemberId, Family = family };
            await SeedDataAsync(family, member1, member2);

            using (var context = new NimblistContext(_dbOptions))
            {
                // ExistingMemberId (non-owner) tries to delete AnotherMemberId's record
                var controller = CreateControllerWithContext(context, ExistingMemberId);
                // Act
                var result = await controller.DeleteFamilyMember(member2RecordId);
                // Assert
                Assert.IsType<ForbidResult>(result);
            }
        }

        [Fact]
        public async Task DeleteFamilyMember_RecordNotFound_ReturnsNotFound()
        {
            // Arrange
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, FamilyOwnerId);
                // Act
                var result = await controller.DeleteFamilyMember(Guid.NewGuid()); // Non-existent ID
                // Assert
                Assert.IsType<NotFoundObjectResult>(result);
            }
        }

        [Fact]
        public async Task DeleteFamilyMember_NoCurrentUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "Some Family", UserId = FamilyOwnerId };
            var memberId = Guid.NewGuid();
            var member = new FamilyMember { Id = memberId, FamilyId = familyId, UserId = ExistingMemberId, Family = family };
            await SeedDataAsync(family, member);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = new FamilyMembersController(context);
                var userWithoutNameIdClaim = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { }, "mockAuth_NoNameId"));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = userWithoutNameIdClaim } };
                // Act
                var result = await controller.DeleteFamilyMember(memberId);
                // Assert
                Assert.IsType<UnauthorizedObjectResult>(result);
            }
        }


        // --- Tests for GetFamilyMember (single record by FamilyMember.Id) ---

        [Fact]
        public async Task GetFamilyMember_FamilyOwnerGetsRecord_ReturnsOk()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "Owner's View", UserId = FamilyOwnerId };
            var memberRecordId = Guid.NewGuid();
            var member = new FamilyMember { Id = memberRecordId, FamilyId = familyId, UserId = ExistingMemberId, Family = family };
            await SeedDataAsync(family, member);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, FamilyOwnerId);
                // Act
                var result = await controller.GetFamilyMember(memberRecordId);
                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var fm = Assert.IsType<FamilyMember>(okResult.Value);
                Assert.Equal(memberRecordId, fm.Id);
            }
        }

        [Fact]
        public async Task GetFamilyMember_MemberGetsOwnRecord_ReturnsOk()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "My Record Fam", UserId = FamilyOwnerId };
            var memberRecordId = Guid.NewGuid();
            var member = new FamilyMember { Id = memberRecordId, FamilyId = familyId, UserId = ExistingMemberId, Family = family };
            await SeedDataAsync(family, member);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ExistingMemberId); // Member gets self
                // Act
                var result = await controller.GetFamilyMember(memberRecordId);
                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                Assert.IsType<FamilyMember>(okResult.Value);
            }
        }

        [Fact]
        public async Task GetFamilyMember_MemberGetsAnotherMemberInSameFamily_ReturnsOk()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "Team Fam", UserId = FamilyOwnerId };
            var member1RecordId = Guid.NewGuid();
            var member1 = new FamilyMember { Id = member1RecordId, FamilyId = familyId, UserId = ExistingMemberId, Family = family };
            var member2RecordId = Guid.NewGuid();
            var member2 = new FamilyMember { Id = member2RecordId, FamilyId = familyId, UserId = AnotherMemberId, Family = family };
            await SeedDataAsync(family, member1, member2);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ExistingMemberId); // ExistingMember views AnotherMember
                // Act
                var result = await controller.GetFamilyMember(member2RecordId);
                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                Assert.IsType<FamilyMember>(okResult.Value);
            }
        }

        [Fact]
        public async Task GetFamilyMember_UserNotInFamilyTriesToGetRecord_ReturnsForbid()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "Private Fam", UserId = FamilyOwnerId };
            var memberRecordId = Guid.NewGuid();
            var member = new FamilyMember { Id = memberRecordId, FamilyId = familyId, UserId = ExistingMemberId, Family = family };
            await SeedDataAsync(family, member);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, UnrelatedUserId);
                // Act
                var result = await controller.GetFamilyMember(memberRecordId);
                // Assert
                Assert.IsType<ForbidResult>(result.Result);
            }
        }

        [Fact]
        public async Task GetFamilyMember_RecordNotFound_ReturnsNotFound()
        {
            // Arrange
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, FamilyOwnerId);
                // Act
                var result = await controller.GetFamilyMember(Guid.NewGuid());
                // Assert
                Assert.IsType<NotFoundObjectResult>(result.Result);
            }
        }

        [Fact]
        public async Task GetFamilyMember_NoCurrentUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var memberRecordId = Guid.NewGuid();
            await SeedDataAsync(new FamilyMember { Id = memberRecordId, FamilyId = Guid.NewGuid(), UserId = ExistingMemberId });
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = new FamilyMembersController(context);
                var userWithoutNameIdClaim = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { }, "mockAuth_NoNameId"));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = userWithoutNameIdClaim } };
                // Act
                var result = await controller.GetFamilyMember(memberRecordId);
                // Assert
                Assert.IsType<UnauthorizedObjectResult>(result.Result);
            }
        }

        // --- Tests for GetFamilyMembersForFamily (list by familyId) ---

        [Fact]
        public async Task GetFamilyMembersForFamily_FamilyOwnerRequests_ReturnsOkWithMembers()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "Owner's Fam List", UserId = FamilyOwnerId };
            var member1 = new FamilyMember { FamilyId = familyId, UserId = ExistingMemberId, Family = family };
            var member2 = new FamilyMember { FamilyId = familyId, UserId = AnotherMemberId, Family = family };
            await SeedDataAsync(family, member1, member2);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, FamilyOwnerId);
                // Act
                var result = await controller.GetFamilyMembersForFamily(familyId);
                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var members = Assert.IsAssignableFrom<IEnumerable<FamilyMember>>(okResult.Value).ToList();
                Assert.Equal(2, members.Count);
                Assert.Contains(members, m => m.UserId == ExistingMemberId);
                Assert.Contains(members, m => m.UserId == AnotherMemberId);
            }
        }

        [Fact]
        public async Task GetFamilyMembersForFamily_FamilyMemberRequests_ReturnsOkWithMembers()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "Member's Fam List", UserId = FamilyOwnerId };
            var member1 = new FamilyMember { FamilyId = familyId, UserId = ExistingMemberId, Family = family }; // Requester
            var member2 = new FamilyMember { FamilyId = familyId, UserId = AnotherMemberId, Family = family }; // Other member
            await SeedDataAsync(family, member1, member2);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ExistingMemberId); // Member requests
                // Act
                var result = await controller.GetFamilyMembersForFamily(familyId);
                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var members = Assert.IsAssignableFrom<IEnumerable<FamilyMember>>(okResult.Value);
                Assert.Equal(2, members.Count());
            }
        }

        [Fact]
        public async Task GetFamilyMembersForFamily_UserNotOwnerAndNotInFamily_ReturnsForbid()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            var family = new Family { Id = familyId, Name = "Forbidden Fam List", UserId = FamilyOwnerId };
            await SeedDataAsync(family);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, UnrelatedUserId);
                // Act
                var result = await controller.GetFamilyMembersForFamily(familyId);
                // Assert
                Assert.IsType<ForbidResult>(result.Result);
            }
        }

        [Fact]
        public async Task GetFamilyMembersForFamily_FamilyNotFound_ReturnsNotFound()
        {
            // Arrange
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, FamilyOwnerId);
                // Act
                var result = await controller.GetFamilyMembersForFamily(Guid.NewGuid()); // Non-existent familyId
                // Assert
                Assert.IsType<NotFoundObjectResult>(result.Result);
            }
        }

        [Fact]
        public async Task GetFamilyMembersForFamily_FamilyHasNoMembers_ReturnsOkWithEmptyList()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            // Family is created by FamilyOwnerId, who is also a member of that family via FamiliesController.PostFamily
            // To test truly "no members", we'd need a scenario where a family exists
            // but has no entries in FamilyMembers table for its FamilyId.
            // The logic of GetFamilyMembersForFamily is: if current user is owner or member, they can view.
            // So, if FamilyOwnerId requests members for a family they own, even if it's empty, they should get an empty list.
            var family = new Family { Id = familyId, Name = "Empty Fam", UserId = FamilyOwnerId };
            await SeedDataAsync(family);
            // Seed no FamilyMember entities for this familyId

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, FamilyOwnerId);
                // Act
                var result = await controller.GetFamilyMembersForFamily(familyId);
                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var members = Assert.IsAssignableFrom<IEnumerable<FamilyMember>>(okResult.Value);
                Assert.Empty(members);
            }
        }

        [Fact]
        public async Task GetFamilyMembersForFamily_NoCurrentUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var familyId = Guid.NewGuid();
            await SeedDataAsync(new Family { Id = familyId, Name = "Auth Test Fam", UserId = FamilyOwnerId });
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = new FamilyMembersController(context);
                var userWithoutNameIdClaim = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { }, "mockAuth_NoNameId"));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = userWithoutNameIdClaim } };
                // Act
                var result = await controller.GetFamilyMembersForFamily(familyId);
                // Assert
                Assert.IsType<UnauthorizedObjectResult>(result.Result);
            }
        }
    }
}