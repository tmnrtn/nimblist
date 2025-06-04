using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Nimblist.api.Controllers;
using Nimblist.api.DTO;
using Nimblist.api.Hubs;
using Nimblist.Data;
using Nimblist.Data.Models;
using Xunit;

namespace Nimblist.test.Controllers
{

    public class ItemsControllerInMemoryTests : IDisposable
    {
        // Mocks for dependencies that are NOT the DbContext
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<IHubContext<ShoppingListHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<ItemsController>> _mockLogger;

        private readonly string _testUserId = "test-user-id";
        private readonly string _otherUserId = "other-user-id";
        private readonly Guid _testListId = Guid.NewGuid();
        private readonly Guid _otherListId = Guid.NewGuid();

        public ItemsControllerInMemoryTests()
        {
            // Set up UserManager mock
            var store = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);

            // Set up SignalR Hub Context mock
            _mockHubContext = new Mock<IHubContext<ShoppingListHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

            // Set up the additional mocks required by the updated constructor
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<ItemsController>>();
        }

        // Helper to create DbContextOptions with a unique InMemory database name
        private DbContextOptions<NimblistContext> CreateNewContextOptions()
        {
            // The key part: Use a unique name for each instance
            var dbName = Guid.NewGuid().ToString();
            return new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
        }

        // Helper to seed data for tests
        private void SeedData(NimblistContext context)
        {
            var userList = new ShoppingList { Id = _testListId, UserId = _testUserId, Name = "User Test List" };
            var otherList = new ShoppingList { Id = _otherListId, UserId = _otherUserId, Name = "Other User List" };

            var items = new List<Item>
        {
            new Item { Id = Guid.NewGuid(), Name = "Milk", Quantity = "1", IsChecked = false, ShoppingListId = _testListId, List = userList, AddedAt = DateTime.UtcNow.AddHours(-1) },
            new Item { Id = Guid.NewGuid(), Name = "Bread", Quantity = "1", IsChecked = true, ShoppingListId = _testListId, List = userList, AddedAt = DateTime.UtcNow.AddHours(-2)  },
            new Item { Id = Guid.NewGuid(), Name = "Eggs", Quantity = "12", IsChecked = false, ShoppingListId = _testListId, List = userList, AddedAt = DateTime.UtcNow },
            new Item { Id = Guid.NewGuid(), Name = "Coffee", Quantity = "1", IsChecked = false, ShoppingListId = otherList.Id, List = otherList, AddedAt = DateTime.UtcNow } // Belongs to another user
        };

            // Important: Add lists first if Items rely on them via navigation properties during add
            context.ShoppingLists.AddRange(userList, otherList);
            context.Items.AddRange(items);
            context.SaveChanges(); // Save seed data
        }

        // Helper to setup Controller with context and user identity
        private ItemsController SetupController(NimblistContext context, string userId)
        {
            var controller = new ItemsController(
                context, 
                _mockUserManager.Object, 
                _mockHubContext.Object, 
                _mockHttpClientFactory.Object,
                _mockConfiguration.Object,
                _mockLogger.Object);

            if (!string.IsNullOrEmpty(userId))
            {
                var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
                {
                new Claim(ClaimTypes.NameIdentifier, userId),
                }, "mock"));

                controller.ControllerContext = new ControllerContext()
                {
                    HttpContext = new DefaultHttpContext() { User = user }
                };
            }
            else // Simulate unauthenticated user or missing claim
            {
                controller.ControllerContext = new ControllerContext()
                {
                    HttpContext = new DefaultHttpContext() { User = new ClaimsPrincipal() } // No identity/claim
                };
            }
            return controller;
        }

        // --- Test Methods ---

        [Fact]
        public async Task GetItems_ReturnsOkResult_WithListOfUserItems()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new NimblistContext(options); // Use 'using' for disposal
            SeedData(context);
            var controller = SetupController(context, _testUserId);
            var expectedCount = await context.Items.CountAsync(i => i.List.UserId == _testUserId); // Count directly from seeded context

            // Act
            var result = await controller.GetItems();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var items = Assert.IsAssignableFrom<IEnumerable<ItemWithCategoryDto>>(okResult.Value);
            Assert.Equal(expectedCount, items.Count());
            Assert.All(items, item => Assert.Equal(_testUserId, item.List.UserId)); // Filter should work
            Assert.Equal("Eggs", items.OrderByDescending(i => i.AddedAt).First().Name); // OrderBy should work
        }

        [Fact]
        public async Task GetItems_ReturnsUnauthorized_WhenUserClaimMissing()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new NimblistContext(options);
            SeedData(context); // Seed data even if user is invalid
            var controller = SetupController(context, null); // Pass null or empty for userId to simulate missing claim

            // Act
            var result = await controller.GetItems();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal("User ID claim not found.", unauthorizedResult.Value);
        }


        [Fact]
        public async Task GetItem_ReturnsOkResult_WithCorrectItem_WhenItemExistsForUser()
        {
            // Arrange
            var options = CreateNewContextOptions();
            Guid testItemId;
            using (var context = new NimblistContext(options))
            {
                SeedData(context);
                testItemId = context.Items.First(i => i.List.UserId == _testUserId).Id; // Get ID from seeded data
            } // Dispose context to ensure controller gets a fresh state if needed (though not strictly required here)

            using (var context = new NimblistContext(options)) // Use a new context instance with the same DB name
            {
                var controller = SetupController(context, _testUserId);

                // Act
                var result = await controller.GetItem(testItemId);

                // Assert
                var actionResult = Assert.IsType<ActionResult<ItemWithCategoryDto>>(result);
                var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
                var itemResult = Assert.IsType<ItemWithCategoryDto>(okResult.Value);
                Assert.Equal(testItemId, itemResult.Id);
                Assert.Equal(_testUserId, itemResult.List.UserId); // Verify correct user
            }
        }

        [Fact]
        public async Task GetItem_ReturnsNotFound_WhenItemDoesNotExist()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new NimblistContext(options);
            SeedData(context); // Seed some other data
            var controller = SetupController(context, _testUserId);
            var nonExistentId = Guid.NewGuid();

            // Act
            var result = await controller.GetItem(nonExistentId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<ItemWithCategoryDto>>(result);
            Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        [Fact]
        public async Task GetItem_ReturnsNotFound_WhenItemBelongsToDifferentUser()
        {
            // Arrange
            var options = CreateNewContextOptions();
            Guid otherUserItemId;
            using (var context = new NimblistContext(options))
            {
                SeedData(context);
                otherUserItemId = context.Items.First(i => i.List.UserId == _otherUserId).Id; // Get ID for other user's item
            }

            using (var context = new NimblistContext(options)) // Use a new context instance
            {
                var controller = SetupController(context, _testUserId); // Controller is for _testUserId

                // Act
                var result = await controller.GetItem(otherUserItemId); // Ask for the other user's item

                // Assert
                // InMemory provider respects the Where clause (i.List.UserId == userId)
                var actionResult = Assert.IsType<ActionResult<ItemWithCategoryDto>>(result);
                Assert.IsType<NotFoundResult>(actionResult.Result);
            }
        }

        [Fact]
        public async Task GetItem_ReturnsUnauthorized_WhenUserClaimMissing()
        {
            // Arrange
            var options = CreateNewContextOptions();
            Guid testItemId;
            using (var context = new NimblistContext(options))
            {
                SeedData(context);
                testItemId = context.Items.First(i => i.List.UserId == _testUserId).Id;
            }

            using (var context = new NimblistContext(options))
            {
                var controller = SetupController(context, null); // No user claim

                // Act
                var result = await controller.GetItem(testItemId);

                // Assert
                var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result); // Controller checks user first
                Assert.Equal("User ID claim not found.", unauthorizedResult.Value);
            }
        }

        [Fact]
        public async Task PostItem_ReturnsCreatedAtAction_AndAddsItem_AndSendsSignalR()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new NimblistContext(options);
            // Seed only the list the item will belong to
            context.ShoppingLists.Add(new ShoppingList { Id = _testListId, UserId = _testUserId, Name = "Test List" });
            context.SaveChanges();

            var controller = SetupController(context, _testUserId);
            var newItemDto = new ItemInputDto { Name = "Cheese", Quantity = "1", IsChecked = false, ShoppingListId = _testListId };
            string expectedGroupName = $"list_{_testListId}";
            var initialCount = await context.Items.CountAsync();

            // Act
            var result = await controller.PostItem(newItemDto);

            // Assert
            // 1. Check Result Type and Location Header
            var actionResult = Assert.IsType<ActionResult<ItemWithCategoryDto>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            Assert.Equal(nameof(ItemsController.GetItem), createdAtActionResult.ActionName);
            var routeValueId = Assert.IsType<Guid>(createdAtActionResult.RouteValues["id"]);

            // 2. Check Response Body
            var itemResult = Assert.IsType<ItemWithCategoryDto>(createdAtActionResult.Value);
            Assert.Equal(newItemDto.Name, itemResult.Name);
            Assert.Equal(newItemDto.ShoppingListId, itemResult.ShoppingListId);
            Assert.Equal(routeValueId, itemResult.Id); // Ensure returned item ID matches route value ID

            // 3. Verify DB Interaction (by checking the context state)
            Assert.Equal(initialCount + 1, await context.Items.CountAsync());
            var addedItem = await context.Items.FindAsync(itemResult.Id);
            Assert.NotNull(addedItem);
            Assert.Equal(newItemDto.Name, addedItem.Name);

            // 4. Verify SignalR Call
            _mockClientProxy.Verify(
                x => x.SendCoreAsync("ReceiveItemAdded", It.Is<object[]>(o => o != null && o.Length == 1 && (o[0] as ItemWithCategoryDto).Id == addedItem.Id), It.IsAny<CancellationToken>()),
                Times.Once);
            _mockClients.Verify(c => c.Group(expectedGroupName), Times.Once);
        }

        [Fact]
        public async Task PutItem_ReturnsNoContent_UpdatesItem_AndSendsSignalR_WhenSuccessful()
        {
            // Arrange
            var options = CreateNewContextOptions();
            Guid existingItemId;
            Guid listId;
            using (var context = new NimblistContext(options))
            {
                SeedData(context);
                var item = context.Items.First(i => i.List.UserId == _testUserId && !i.IsChecked);
                existingItemId = item.Id;
                listId = item.ShoppingListId;
            }

            using (var context = new NimblistContext(options)) // Use a new context for the test action
            {
                var controller = SetupController(context, _testUserId);
                var updateDto = new ItemUpdateDto { Name = "Updated Milk", Quantity = "2", IsChecked = true, ShoppingListId = listId };
                string expectedGroupName = $"list_{listId}";

                // Act
                var result = await controller.PutItem(existingItemId, updateDto);

                // Assert
                // 1. Check Result Type
                Assert.IsType<NoContentResult>(result);

                // 2. Verify DB Interaction (by checking the state)
                var updatedItem = await context.Items.FindAsync(existingItemId);
                Assert.NotNull(updatedItem);
                Assert.Equal(updateDto.Name, updatedItem.Name);
                Assert.Equal(updateDto.Quantity, updatedItem.Quantity);
                Assert.Equal(updateDto.IsChecked, updatedItem.IsChecked);

                // 3. Verify SignalR Call
                _mockClientProxy.Verify(
                    x => x.SendCoreAsync("ReceiveItemUpdated", It.Is<object[]>(o => o != null && o.Length == 1 && (o[0] as ItemWithCategoryDto).Id == existingItemId && (o[0] as ItemWithCategoryDto).Name == updateDto.Name), It.IsAny<CancellationToken>()),
                    Times.Once);
                _mockClients.Verify(c => c.Group(expectedGroupName), Times.Once);
            }
        }

        [Fact]
        public async Task PutItem_ReturnsNotFound_WhenItemDoesNotExist()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new NimblistContext(options);
            SeedData(context); // Seed some data, but not the item we'll try to update
            var controller = SetupController(context, _testUserId);
            var nonExistentId = Guid.NewGuid();
            var updateDto = new ItemUpdateDto { Name = "Doesn't Matter", Quantity = "1", IsChecked = false, ShoppingListId = _testListId };

            // Act
            var result = await controller.PutItem(nonExistentId, updateDto);

            // Assert
            Assert.IsType<NotFoundResult>(result);
            _mockClientProxy.Verify(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PutItem_ReturnsNotFound_WhenItemBelongsToDifferentUser()
        {
            // Arrange
            var options = CreateNewContextOptions();
            Guid otherUserItemId;
            Guid otherListId;
            using (var context = new NimblistContext(options))
            {
                SeedData(context);
                var item = context.Items.Include(i => i.List).First(i => i.List.UserId == _otherUserId);
                otherUserItemId = item.Id;
                otherListId = item.ShoppingListId;
            }

            using (var context = new NimblistContext(options))
            {
                var controller = SetupController(context, _testUserId); // Controller for _testUserId
                var updateDto = new ItemUpdateDto { Name = "No Update", Quantity = "1", IsChecked = false, ShoppingListId = otherListId };

                // Act
                var result = await controller.PutItem(otherUserItemId, updateDto); // Try to update other user's item

                // Assert
                Assert.IsType<NotFoundResult>(result); // Not found because query includes List.UserId == _testUserId
                _mockClientProxy.Verify(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }


        [Fact]
        public async Task DeleteItem_ReturnsNoContent_RemovesItem_AndSendsSignalR_WhenSuccessful()
        {
            // Arrange
            var options = CreateNewContextOptions();
            Guid itemToDeleteId;
            Guid listId;
            using (var context = new NimblistContext(options))
            {
                SeedData(context);
                var item = context.Items.Include(i => i.List).First(i => i.List.UserId == _testUserId);
                itemToDeleteId = item.Id;
                listId = item.ShoppingListId;
            }

            using (var context = new NimblistContext(options)) // New context for the action
            {
                var controller = SetupController(context, _testUserId);
                string expectedGroupName = $"list_{listId}";
                var initialCount = await context.Items.CountAsync(); // Count before delete

                // Act
                var result = await controller.DeleteItem(itemToDeleteId);

                // Assert
                // 1. Check Result Type
                Assert.IsType<NoContentResult>(result);

                // 2. Verify DB Interaction (by checking state)
                Assert.Equal(initialCount - 1, await context.Items.CountAsync()); // Count should decrease
                var deletedItem = await context.Items.FindAsync(itemToDeleteId);
                Assert.Null(deletedItem); // Item should no longer be found

                // 3. Verify SignalR Call
                _mockClientProxy.Verify(
                    x => x.SendCoreAsync("ReceiveItemDeleted", It.Is<object[]>(o => o != null && o.Length == 1 && (Guid)o[0] == itemToDeleteId), It.IsAny<CancellationToken>()),
                    Times.Once);
                _mockClients.Verify(c => c.Group(expectedGroupName), Times.Once);
            }
        }

        [Fact]
        public async Task DeleteItem_ReturnsNotFound_WhenItemDoesNotExist()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new NimblistContext(options);
            SeedData(context); // Seed some other data
            var controller = SetupController(context, _testUserId);
            var nonExistentId = Guid.NewGuid();

            // Act
            var result = await controller.DeleteItem(nonExistentId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
            _mockClientProxy.Verify(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DeleteItem_ReturnsNotFound_WhenItemBelongsToDifferentUser()
        {
            // Arrange
            var options = CreateNewContextOptions();
            Guid otherUserItemId;
            using (var context = new NimblistContext(options))
            {
                SeedData(context);
                otherUserItemId = context.Items.Include(i => i.List).First(i => i.List.UserId == _otherUserId).Id;
            }

            using (var context = new NimblistContext(options))
            {
                var controller = SetupController(context, _testUserId); // Controller for _testUserId

                // Act
                var result = await controller.DeleteItem(otherUserItemId); // Try delete other user's item

                // Assert
                Assert.IsType<NotFoundResult>(result); // Not found because query includes List.UserId == _testUserId
                var itemStillExists = await context.Items.FindAsync(otherUserItemId);
                Assert.NotNull(itemStillExists); // Verify it wasn't actually deleted
                _mockClientProxy.Verify(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        // Implement IDisposable if you need cleanup *after* all tests in the class run
        public void Dispose()
        {
            // Cleanup if necessary (e.g., if using a shared context instance)
        }
    }
}