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
    public class ListSharesControllerTests
    {
        private readonly DbContextOptions<NimblistContext> _dbOptions;

        // Define User IDs for different roles in tests
        private const string ListOwnerUserId = "list-owner-user-id";
        private const string UserToShareWithId = "user-to-share-with-id";
        private const string AnotherUserId = "another-user-id";
        private const string FamilyMemberUserId = "family-member-user-id";

        public ListSharesControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            SeedInitialUsers(); // Important for FK constraints on User references
        }

        private void SeedInitialUsers()
        {
            using (var context = new NimblistContext(_dbOptions))
            {
                var userIds = new[] { ListOwnerUserId, UserToShareWithId, AnotherUserId, FamilyMemberUserId };
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

        private ListSharesController CreateControllerWithContext(NimblistContext context, string currentUserId)
        {
            var controller = new ListSharesController(context);
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

        // --- Tests for PostListShare ---

        [Fact]
        public async Task PostListShare_WithValidUserIdToShareWith_ReturnsCreatedAtAction()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            await SeedDataAsync(shoppingList);

            var dto = new ListShareInputDto { ListId = listId, UserIdToShareWith = UserToShareWithId };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.PostListShare(dto);
                
                // Assert
                var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
                var listShare = Assert.IsType<ListShare>(createdAtActionResult.Value);
                Assert.Equal(listId, listShare.ListId);
                Assert.Equal(UserToShareWithId, listShare.UserId);
                Assert.Null(listShare.FamilyId);
                Assert.Equal(nameof(controller.GetListShare), createdAtActionResult.ActionName);

                // Verify in DB
                var dbShare = await context.ListShares.FirstOrDefaultAsync(ls => ls.ListId == listId && ls.UserId == UserToShareWithId);
                Assert.NotNull(dbShare);
            }
        }

        [Fact]
        public async Task PostListShare_WithValidFamilyIdToShareWith_ReturnsCreatedAtAction()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var familyId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            var family = new Family { Id = familyId, Name = "Test Family", UserId = AnotherUserId };
            await SeedDataAsync(shoppingList, family);

            var dto = new ListShareInputDto { ListId = listId, FamilyIdToShareWith = familyId };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.PostListShare(dto);
                
                // Assert
                var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
                var listShare = Assert.IsType<ListShare>(createdAtActionResult.Value);
                Assert.Equal(listId, listShare.ListId);
                Assert.Equal(familyId, listShare.FamilyId);
                Assert.Null(listShare.UserId);
                Assert.Equal(nameof(controller.GetListShare), createdAtActionResult.ActionName);

                // Verify in DB
                var dbShare = await context.ListShares.FirstOrDefaultAsync(ls => ls.ListId == listId && ls.FamilyId == familyId);
                Assert.NotNull(dbShare);
            }
        }

        [Fact]
        public async Task PostListShare_NoUserIdOrFamilyId_ReturnsBadRequest()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            await SeedDataAsync(shoppingList);

            var dto = new ListShareInputDto { ListId = listId };  // No UserIdToShareWith or FamilyIdToShareWith

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.PostListShare(dto);
                
                // Assert
                var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
                Assert.Contains("Either UserIdToShareWith or FamilyIdToShareWith must be provided", badRequestResult.Value.ToString());
            }
        }

        [Fact]
        public async Task PostListShare_BothUserIdAndFamilyId_ReturnsBadRequest()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var familyId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            var family = new Family { Id = familyId, Name = "Test Family", UserId = AnotherUserId };
            await SeedDataAsync(shoppingList, family);

            var dto = new ListShareInputDto { ListId = listId, UserIdToShareWith = UserToShareWithId, FamilyIdToShareWith = familyId };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.PostListShare(dto);
                
                // Assert
                var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
                Assert.Contains("Cannot provide both UserIdToShareWith and FamilyIdToShareWith", badRequestResult.Value.ToString());
            }
        }

        [Fact]
        public async Task PostListShare_ShoppingListNotFound_ReturnsNotFound()
        {
            // Arrange
            var nonExistentListId = Guid.NewGuid();
            var dto = new ListShareInputDto { ListId = nonExistentListId, UserIdToShareWith = UserToShareWithId };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.PostListShare(dto);
                
                // Assert
                var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
                Assert.Contains("Shopping list not found", notFoundResult.Value.ToString());
            }
        }

        [Fact]
        public async Task PostListShare_UserIsNotListOwner_ReturnsForbid()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            await SeedDataAsync(shoppingList);

            var dto = new ListShareInputDto { ListId = listId, UserIdToShareWith = UserToShareWithId };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, AnotherUserId); // Not the list owner
                
                // Act
                var result = await controller.PostListShare(dto);
                
                // Assert
                Assert.IsType<ForbidResult>(result.Result);
            }
        }

        [Fact]
        public async Task PostListShare_SharingWithListOwner_ReturnsBadRequest()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            await SeedDataAsync(shoppingList);

            var dto = new ListShareInputDto { ListId = listId, UserIdToShareWith = ListOwnerUserId }; // Sharing with owner

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.PostListShare(dto);
                
                // Assert
                var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
                Assert.Contains("Cannot share a list with its owner directly", badRequestResult.Value.ToString());
            }
        }

        [Fact]
        public async Task PostListShare_DuplicateUserShare_ReturnsConflict()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            var existingShare = new ListShare { ListId = listId, UserId = UserToShareWithId };
            await SeedDataAsync(shoppingList, existingShare);

            var dto = new ListShareInputDto { ListId = listId, UserIdToShareWith = UserToShareWithId };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.PostListShare(dto);
                
                // Assert
                var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
                Assert.Contains("This list is already shared with the specified user", conflictResult.Value.ToString());
            }
        }

        [Fact]
        public async Task PostListShare_DuplicateFamilyShare_ReturnsConflict()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var familyId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            var family = new Family { Id = familyId, Name = "Test Family", UserId = AnotherUserId };
            var existingShare = new ListShare { ListId = listId, FamilyId = familyId };
            await SeedDataAsync(shoppingList, family, existingShare);

            var dto = new ListShareInputDto { ListId = listId, FamilyIdToShareWith = familyId };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.PostListShare(dto);
                
                // Assert
                var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
                Assert.Contains("This list is already shared with the specified user or family", conflictResult.Value.ToString());
            }
        }

        [Fact]
        public async Task PostListShare_UserToShareWithNotFound_ReturnsBadRequest()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            await SeedDataAsync(shoppingList);

            var dto = new ListShareInputDto { ListId = listId, UserIdToShareWith = "non-existent-user-id" };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.PostListShare(dto);
                
                // Assert
                var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
                Assert.Contains("User to share with not found", badRequestResult.Value.ToString());
            }
        }

        [Fact]
        public async Task PostListShare_FamilyToShareWithNotFound_ReturnsBadRequest()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var nonExistentFamilyId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            await SeedDataAsync(shoppingList);

            var dto = new ListShareInputDto { ListId = listId, FamilyIdToShareWith = nonExistentFamilyId };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.PostListShare(dto);
                
                // Assert
                var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
                Assert.Contains("Family to share with not found", badRequestResult.Value.ToString());
            }
        }

        [Fact]
        public async Task PostListShare_NoUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var dto = new ListShareInputDto { ListId = Guid.NewGuid(), UserIdToShareWith = UserToShareWithId };
            
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = new ListSharesController(context);
                var userWithoutNameIdClaim = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { }, "mockAuth_NoNameId"));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = userWithoutNameIdClaim } };
                
                // Act
                var result = await controller.PostListShare(dto);
                
                // Assert
                var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
                Assert.Contains("User ID claim not found", unauthorizedResult.Value.ToString());
            }
        }

        // --- Tests for DeleteListShare ---

        [Fact]
        public async Task DeleteListShare_ListOwnerDeletesShare_ReturnsNoContent()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var shareId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            var listShare = new ListShare { Id = shareId, ListId = listId, UserId = UserToShareWithId };
            await SeedDataAsync(shoppingList, listShare);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.DeleteListShare(shareId);
                
                // Assert
                Assert.IsType<NoContentResult>(result);

                // Verify in DB
                var dbShare = await context.ListShares.FindAsync(shareId);
                Assert.Null(dbShare); // Share should be deleted
            }
        }

        [Fact]
        public async Task DeleteListShare_ShareNotFound_ReturnsNotFound()
        {
            // Arrange
            var nonExistentShareId = Guid.NewGuid();

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.DeleteListShare(nonExistentShareId);
                
                // Assert
                var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
                Assert.Contains("List share record not found", notFoundResult.Value.ToString());
            }
        }

        [Fact]
        public async Task DeleteListShare_NonOwnerTriesToDelete_ReturnsForbid()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var shareId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            var listShare = new ListShare { Id = shareId, ListId = listId, UserId = UserToShareWithId };
            await SeedDataAsync(shoppingList, listShare);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, AnotherUserId); // Not the list owner
                
                // Act
                var result = await controller.DeleteListShare(shareId);
                
                // Assert
                Assert.IsType<ForbidResult>(result);

                // Verify share still exists
                var dbShare = await context.ListShares.FindAsync(shareId);
                Assert.NotNull(dbShare);
            }
        }

        [Fact]
        public async Task DeleteListShare_NoUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var shareId = Guid.NewGuid();
            
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = new ListSharesController(context);
                var userWithoutNameIdClaim = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { }, "mockAuth_NoNameId"));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = userWithoutNameIdClaim } };
                
                // Act
                var result = await controller.DeleteListShare(shareId);
                
                // Assert
                var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
                Assert.Contains("User ID claim not found", unauthorizedResult.Value.ToString());
            }
        }

        // --- Tests for GetListShare ---

        [Fact]
        public async Task GetListShare_ListOwnerGetsShare_ReturnsOk()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var shareId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            var listShare = new ListShare { Id = shareId, ListId = listId, UserId = UserToShareWithId };
            await SeedDataAsync(shoppingList, listShare);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.GetListShare(shareId);
                
                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var returnedShare = Assert.IsType<ListShare>(okResult.Value);
                Assert.Equal(shareId, returnedShare.Id);
                Assert.Equal(listId, returnedShare.ListId);
                Assert.Equal(UserToShareWithId, returnedShare.UserId);
            }
        }

        [Fact]
        public async Task GetListShare_SharedUserGetsShare_ReturnsOk()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var shareId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            var listShare = new ListShare { Id = shareId, ListId = listId, UserId = UserToShareWithId };
            await SeedDataAsync(shoppingList, listShare);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, UserToShareWithId);
                
                // Act
                var result = await controller.GetListShare(shareId);
                
                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var returnedShare = Assert.IsType<ListShare>(okResult.Value);
                Assert.Equal(shareId, returnedShare.Id);
                Assert.Equal(UserToShareWithId, returnedShare.UserId);
            }
        }

        [Fact]
        public async Task GetListShare_FamilyMemberGetsShare_ReturnsOk()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var familyId = Guid.NewGuid();
            var shareId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            var family = new Family { Id = familyId, Name = "Test Family", UserId = AnotherUserId };
            var familyMember = new FamilyMember { FamilyId = familyId, UserId = FamilyMemberUserId };
            var listShare = new ListShare { Id = shareId, ListId = listId, FamilyId = familyId };
            await SeedDataAsync(shoppingList, family, familyMember, listShare);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, FamilyMemberUserId);
                
                // Act
                var result = await controller.GetListShare(shareId);
                
                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var returnedShare = Assert.IsType<ListShare>(okResult.Value);
                Assert.Equal(shareId, returnedShare.Id);
                Assert.Equal(familyId, returnedShare.FamilyId);
            }
        }

        [Fact]
        public async Task GetListShare_UnrelatedUserGetsShare_ReturnsForbid()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var shareId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            var listShare = new ListShare { Id = shareId, ListId = listId, UserId = UserToShareWithId };
            await SeedDataAsync(shoppingList, listShare);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, AnotherUserId); // Neither owner nor shared user
                
                // Act
                var result = await controller.GetListShare(shareId);
                
                // Assert
                Assert.IsType<ForbidResult>(result.Result);
            }
        }

        [Fact]
        public async Task GetListShare_ShareNotFound_ReturnsNotFound()
        {
            // Arrange
            var nonExistentShareId = Guid.NewGuid();

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.GetListShare(nonExistentShareId);
                
                // Assert
                var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
                Assert.Contains("List share record not found", notFoundResult.Value.ToString());
            }
        }

        [Fact]
        public async Task GetListShare_NoUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var shareId = Guid.NewGuid();
            
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = new ListSharesController(context);
                var userWithoutNameIdClaim = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { }, "mockAuth_NoNameId"));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = userWithoutNameIdClaim } };
                
                // Act
                var result = await controller.GetListShare(shareId);
                
                // Assert
                var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
                Assert.Contains("User ID claim not found", unauthorizedResult.Value.ToString());
            }
        }

        // --- Tests for GetSharesForList ---

        [Fact]
        public async Task GetSharesForList_ListOwnerGetsShares_ReturnsOkWithShares()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            var share1 = new ListShare { Id = Guid.NewGuid(), ListId = listId, UserId = UserToShareWithId };
            var share2 = new ListShare { Id = Guid.NewGuid(), ListId = listId, UserId = AnotherUserId };
            await SeedDataAsync(shoppingList, share1, share2);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.GetSharesForList(listId);
                
                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var shares = Assert.IsAssignableFrom<IEnumerable<ListShare>>(okResult.Value);
                Assert.Equal(2, shares.Count());
                Assert.Contains(shares, s => s.UserId == UserToShareWithId);
                Assert.Contains(shares, s => s.UserId == AnotherUserId);
            }
        }

        [Fact]
        public async Task GetSharesForList_NonOwnerGetsShares_ReturnsForbid()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, Name = "Test List", UserId = ListOwnerUserId };
            var share = new ListShare { Id = Guid.NewGuid(), ListId = listId, UserId = UserToShareWithId };
            await SeedDataAsync(shoppingList, share);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, UserToShareWithId); // Not the owner
                
                // Act
                var result = await controller.GetSharesForList(listId);
                
                // Assert
                Assert.IsType<ForbidResult>(result.Result);
            }
        }

        [Fact]
        public async Task GetSharesForList_ListNotFound_ReturnsNotFound()
        {
            // Arrange
            var nonExistentListId = Guid.NewGuid();

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context, ListOwnerUserId);
                
                // Act
                var result = await controller.GetSharesForList(nonExistentListId);
                
                // Assert
                var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
                Assert.Contains("Shopping list not found", notFoundResult.Value.ToString());
            }
        }

        [Fact]
        public async Task GetSharesForList_NoUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var listId = Guid.NewGuid();
            
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = new ListSharesController(context);
                var userWithoutNameIdClaim = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { }, "mockAuth_NoNameId"));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = userWithoutNameIdClaim } };
                
                // Act
                var result = await controller.GetSharesForList(listId);
                
                // Assert
                var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
                Assert.Contains("User ID claim not found", unauthorizedResult.Value.ToString());
            }
        }
    }
}

