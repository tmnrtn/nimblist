using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Required for In-Memory provider and options

// Moq is no longer needed for DbContext/DbSet, but might be needed if other dependencies exist
// using Moq;
using Nimblist.api.Controllers; // Assuming this is the correct namespace
using Nimblist.api.DTO;        // Assuming this is the correct namespace
using Nimblist.Data;
using Nimblist.Data.Models;
using Xunit;

namespace Nimblist.test.Controllers
{
    public class ShoppingListsControllerTests // Consider : IDisposable if complex setup/teardown needed later
    {
        // No longer need Moq fields for context/dbset
        // private readonly Mock<NimblistContext> _mockContext;
        // private readonly Mock<DbSet<ShoppingList>> _mockSet;

        // Store options to create contexts
        private readonly DbContextOptions<NimblistContext> _dbOptions;
        private const string TestUserId = "test-user-id"; // Define constant for user ID

        public ShoppingListsControllerTests()
        {
            // Create unique DbContextOptions for each test instance using the In-Memory provider
            _dbOptions = new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB name ensures isolation
                .Options;
        }

        // Helper method to seed data into a fresh context instance
        private async Task SeedDataAsync(params object[] entities)
        {
            using (var context = new NimblistContext(_dbOptions))
            {
                context.AddRange(entities);
                await context.SaveChangesAsync();
            }
        }

        // Helper method to create the controller with a fresh context and set user context
        private ShoppingListsController CreateControllerWithContext(NimblistContext context)
        {
            var controller = new ShoppingListsController(context);

            // Setup the User claims principal
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId)
            }, "mock"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            return controller;
        }

        [Fact]
        public async Task GetShoppingLists_ReturnsOkResult_WithUserShoppingLists()
        {
            // Arrange
            var userListId = Guid.NewGuid();
            var otherUserId = "other-user-id";
            var otherUserListId = Guid.NewGuid();

            await SeedDataAsync(
                new ShoppingList { Id = userListId, Name = "My Test List", UserId = TestUserId },
                new ShoppingList { Id = otherUserListId, Name = "Other User's List", UserId = otherUserId }
            );

            await SeedDataAsync(new ListShare { Id = Guid.NewGuid(), ListId = userListId, UserId = TestUserId });

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.GetShoppingLists();

                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var returnValue = Assert.IsAssignableFrom<IEnumerable<ShoppingListWithItemsDto>>(okResult.Value); // Use IEnumerable
                Assert.Single(returnValue); // Should only contain the list for TestUserId
                Assert.Equal("My Test List", returnValue.First().Name);
                Assert.Equal(userListId, returnValue.First().Id);
            }
        }

        [Fact]
        public async Task GetShoppingList_ReturnsNotFound_WhenListDoesNotExist()
        {
            // Arrange
            var nonExistentListId = Guid.NewGuid();
            // No need to seed anything for this specific test case, or seed unrelated data
            await SeedDataAsync(new ShoppingList { Id = Guid.NewGuid(), Name = "Some other list", UserId = TestUserId });


            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.GetShoppingList(nonExistentListId);

                // Assert
                Assert.IsType<NotFoundResult>(result.Result);
            }
        }

        [Fact]
        public async Task GetShoppingList_ReturnsNotFound_WhenListBelongsToOtherUser()
        {
            // Arrange
            var otherUserId = "other-user-id";
            var listId = Guid.NewGuid();
            await SeedDataAsync(new ShoppingList { Id = listId, Name = "Other User's List", UserId = otherUserId });

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context); // Controller context user is TestUserId

                // Act
                var result = await controller.GetShoppingList(listId); // Try to get other user's list

                // Assert
                // Assuming the controller checks ownership and returns NotFound if user doesn't match
                Assert.IsType<NotFoundResult>(result.Result);
            }
        }

        [Fact]
        public async Task GetShoppingList_ReturnsOkResult_WhenListExistsAndBelongsToUser()
        {
            // Arrange
            var listId = Guid.NewGuid();
            await SeedDataAsync(new ShoppingList { Id = listId, Name = "My Specific List", UserId = TestUserId });
            await SeedDataAsync(new ListShare { Id = Guid.NewGuid(), ListId = listId, UserId = TestUserId });

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.GetShoppingList(listId);

                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var returnValue = Assert.IsType<ShoppingListWithItemsDto>(okResult.Value);
                Assert.Equal(listId, returnValue.Id);
                Assert.Equal(TestUserId, returnValue.UserId);
            }
        }


        [Fact]
        public async Task PostShoppingList_ReturnsCreatedAtActionResult_AndAddsToList()
        {
            // Arrange
            var newListDto = new ShoppingListInputDto { Name = "New List From Post" };
            Guid createdListId = Guid.Empty; // To store the ID for verification

            using (var context = new NimblistContext(_dbOptions)) // Context for Act
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.PostShoppingList(newListDto);

                // Assert (Result Check)
                var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
                var returnValue = Assert.IsType<ShoppingList>(createdAtActionResult.Value);
                Assert.Equal(newListDto.Name, returnValue.Name);
                Assert.Equal(TestUserId, returnValue.UserId); // Ensure user ID is set correctly
                Assert.Equal(nameof(controller.GetShoppingList), createdAtActionResult.ActionName); // Check action name
                createdListId = returnValue.Id; // Store ID for verification step
            } // Dispose the context used for the action

            // Assert (Side Effect Verification)
            using (var context = new NimblistContext(_dbOptions)) // New context to verify save
            {
                var addedList = await context.ShoppingLists.FindAsync(createdListId);
                Assert.NotNull(addedList);
                Assert.Equal(newListDto.Name, addedList.Name);
                Assert.Equal(TestUserId, addedList.UserId);
            }
        }

        [Fact]
        public async Task DeleteShoppingList_ReturnsNoContent_WhenListIsDeleted()
        {
            // Arrange
            var listIdToDelete = Guid.NewGuid();
            await SeedDataAsync(new ShoppingList { Id = listIdToDelete, Name = "List To Delete", UserId = TestUserId });
            await SeedDataAsync(new ListShare { Id = Guid.NewGuid(), ListId = listIdToDelete, UserId = TestUserId });

            using (var context = new NimblistContext(_dbOptions)) // Context for Act
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.DeleteShoppingList(listIdToDelete);

                // Assert (Result Check)
                Assert.IsType<NoContentResult>(result);

            } // Dispose the context used for the action

            // Assert (Side Effect Verification)
            using (var context = new NimblistContext(_dbOptions)) // New context to verify save
            {
                var deletedList = await context.ShoppingLists.FindAsync(listIdToDelete);
                Assert.Null(deletedList); // Verify it's gone from the database
            }
        }

        [Fact]
        public async Task DeleteShoppingList_ReturnsNotFound_WhenListDoesNotExist()
        {
            // Arrange
            var nonExistentListId = Guid.NewGuid();
            // Don't seed the list we are trying to delete

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.DeleteShoppingList(nonExistentListId);

                // Assert
                Assert.IsType<NotFoundResult>(result);
            }
        }

        [Fact]
        public async Task DeleteShoppingList_ReturnsNotFound_WhenListBelongsToOtherUser()
        {
            // Arrange
            var otherUserId = "other-user-id";
            var listIdToDelete = Guid.NewGuid();
            await SeedDataAsync(new ShoppingList { Id = listIdToDelete, Name = "Other User's List", UserId = otherUserId });


            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context); // Controller user is TestUserId

                // Act
                var result = await controller.DeleteShoppingList(listIdToDelete); // Try deleting other user's list

                // Assert
                // Assuming controller checks ownership
                Assert.IsType<NotFoundResult>(result);
            }

            // Assert (Side Effect Verification - Ensure it wasn't deleted)
            using (var context = new NimblistContext(_dbOptions))
            {
                var listShouldStillExist = await context.ShoppingLists.FindAsync(listIdToDelete);
                Assert.NotNull(listShouldStillExist);
            }
        }
        [Fact]
        public async Task PutShoppingList_ReturnsNoContent_WhenListIsUpdated()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var initialName = "Initial List Name";
            var updatedName = "Updated List Name";

            await SeedDataAsync(new ShoppingList { Id = listId, Name = initialName, UserId = TestUserId });
            await SeedDataAsync(new ListShare { Id = Guid.NewGuid(), ListId = listId, UserId = TestUserId });

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);
                var updateDto = new ShoppingListUpdateDto { Name = updatedName };

                // Act
                var result = await controller.PutShoppingList(listId, updateDto);

                // Assert
                Assert.IsType<NoContentResult>(result);

                // Verify the update in the database
                var updatedList = await context.ShoppingLists.FindAsync(listId);
                Assert.NotNull(updatedList);
                Assert.Equal(updatedName, updatedList.Name);
            }
        }

        [Fact]
        public async Task PutShoppingList_ReturnsNotFound_WhenListDoesNotExist()
        {
            // Arrange
            var nonExistentListId = Guid.NewGuid();
            var updateDto = new ShoppingListUpdateDto { Name = "Updated Name" };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.PutShoppingList(nonExistentListId, updateDto);

                // Assert
                Assert.IsType<NotFoundResult>(result);
            }
        }

        [Fact]
        public async Task PutShoppingList_ReturnsNotFound_WhenListBelongsToOtherUser()
        {
            // Arrange
            var otherUserId = "other-user-id";
            var listId = Guid.NewGuid();
            await SeedDataAsync(new ShoppingList { Id = listId, Name = "Other User's List", UserId = otherUserId });

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);
                var updateDto = new ShoppingListUpdateDto { Name = "Updated Name" };

                // Act
                var result = await controller.PutShoppingList(listId, updateDto);

                // Assert
                Assert.IsType<NotFoundResult>(result);
            }
        }

        [Fact]
        public async Task PutShoppingList_ReturnsUnauthorized_WhenUserIdClaimIsMissing()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var updateDto = new ShoppingListUpdateDto { Name = "Updated Name" };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = new ShoppingListsController(context);

                // Set up a ClaimsPrincipal without the NameIdentifier claim
                var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { }, "mock"));
                controller.ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = user }
                };

                // Act
                var result = await controller.PutShoppingList(listId, updateDto);

                // Assert
                var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
                Assert.Equal("User ID claim not found.", unauthorizedResult.Value);
            }
        }
        [Fact]
        public async Task GetUserShoppingLists_ReturnsOnlyOwnedAndSharedLists()
        {
            // Arrange
            var userId = TestUserId;
            var sharedUserId = "shared-user-id";
            var ownedListId = Guid.NewGuid();
            var sharedListId = Guid.NewGuid();

            await SeedDataAsync(
                new ShoppingList { Id = ownedListId, Name = "Owned List", UserId = userId },
                new ShoppingList { Id = sharedListId, Name = "Shared List", UserId = sharedUserId }
            );

            await SeedDataAsync(new ListShare { Id = Guid.NewGuid(), ListId = ownedListId, UserId = TestUserId });
            await SeedDataAsync(new ListShare { Id = Guid.NewGuid(), ListId = sharedListId, UserId = TestUserId });


            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.GetShoppingLists();

                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var returnValue = Assert.IsAssignableFrom<IEnumerable<ShoppingListWithItemsDto>>(okResult.Value);
                Assert.Equal(2, returnValue.Count());
                Assert.Contains(returnValue, list => list.Id == ownedListId);
                Assert.Contains(returnValue, list => list.Id == sharedListId);
            }
        }
        [Fact]
        public async Task GetShoppingList_ReturnsSharedList_WhenUserHasAccess()
        {
            // Arrange
            var sharedUserId = "shared-user-id";
            var sharedListId = Guid.NewGuid();
            await SeedDataAsync(new ShoppingList { Id = sharedListId, Name = "Shared List", UserId = sharedUserId });
            await SeedDataAsync(new ListShare { Id = Guid.NewGuid(), ListId = sharedListId, UserId = TestUserId });
            await SeedDataAsync(new ListShare { Id = Guid.NewGuid(), ListId = sharedListId, UserId = sharedUserId });

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.GetShoppingList(sharedListId);

                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var returnValue = Assert.IsType<ShoppingListWithItemsDto>(okResult.Value);
                Assert.Equal(sharedListId, returnValue.Id);
            }
        }
        [Fact]
        public async Task DeleteShoppingList_ReturnsNotFound_WhenDeletingSharedList()
        {
            // Arrange
            var sharedUserId = "shared-user-id";
            var sharedListId = Guid.NewGuid();
            await SeedDataAsync(new ShoppingList { Id = sharedListId, Name = "Shared List", UserId = sharedUserId });

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.DeleteShoppingList(sharedListId);

                // Assert
                Assert.IsType<NotFoundResult>(result);
            }
        }
        [Fact]
        public async Task PostShoppingList_CreatesListWithCorrectUserId()
        {
            // Arrange
            var newListDto = new ShoppingListInputDto { Name = "New List" };

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.PostShoppingList(newListDto);

                // Assert
                var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
                var returnValue = Assert.IsType<ShoppingList>(createdAtActionResult.Value);
                Assert.Equal(TestUserId, returnValue.UserId);
                Assert.Equal(newListDto.Name, returnValue.Name);
            }
        }
    }
}