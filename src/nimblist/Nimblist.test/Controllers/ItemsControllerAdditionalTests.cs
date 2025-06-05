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
using Microsoft.Extensions.Options;
using Moq;
using Nimblist.api.Controllers;
using Nimblist.api.DTO;
using Nimblist.api.Hubs;
using Nimblist.Data;
using Nimblist.Data.Models;
using Xunit;

namespace Nimblist.test.Controllers
{
    /// <summary>
    /// Additional tests for ItemsController to improve test coverage
    /// </summary>
    public class ItemsControllerAdditionalTests : IDisposable
    {
        // Mocks for dependencies
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

        public ItemsControllerAdditionalTests()
        {
            // Set up UserManager mock with non-null parameters
            var store = new Mock<IUserStore<ApplicationUser>>();
            var options = new Mock<IOptions<IdentityOptions>>();
            var passwordHasher = new Mock<IPasswordHasher<ApplicationUser>>();
            var userValidators = new List<IUserValidator<ApplicationUser>>();
            var passwordValidators = new List<IPasswordValidator<ApplicationUser>>();
            var keyNormalizer = new Mock<ILookupNormalizer>();
            var errors = new Mock<IdentityErrorDescriber>();
            var services = new Mock<IServiceProvider>();
            var logger = new Mock<ILogger<UserManager<ApplicationUser>>>();

            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                store.Object,
                options.Object,
                passwordHasher.Object,
                userValidators,
                passwordValidators,
                keyNormalizer.Object,
                errors.Object,
                services.Object,
                logger.Object);

            // Set up SignalR Hub Context mock
            _mockHubContext = new Mock<IHubContext<ShoppingListHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

            // Set up additional mocks
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<ItemsController>>();
        }

        // Helper to create DbContextOptions with a unique InMemory database name
        private DbContextOptions<NimblistContext> CreateNewContextOptions()
        {
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

            // Create category and subcategory
            var dairyCategory = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
            var milkSubCategory = new SubCategory { Id = Guid.NewGuid(), Name = "Milk", ParentCategoryId = dairyCategory.Id };

            var items = new List<Item>
            {
                new Item { Id = Guid.NewGuid(), Name = "Milk", Quantity = "1", IsChecked = false, ShoppingListId = _testListId, List = userList, AddedAt = DateTime.UtcNow.AddHours(-1), CategoryId = dairyCategory.Id, SubCategoryId = milkSubCategory.Id },
                new Item { Id = Guid.NewGuid(), Name = "Bread", Quantity = "1", IsChecked = true, ShoppingListId = _testListId, List = userList, AddedAt = DateTime.UtcNow.AddHours(-2) },
                new Item { Id = Guid.NewGuid(), Name = "Eggs", Quantity = "12", IsChecked = false, ShoppingListId = _testListId, List = userList, AddedAt = DateTime.UtcNow },
                new Item { Id = Guid.NewGuid(), Name = "Coffee", Quantity = "1", IsChecked = false, ShoppingListId = _otherListId, List = otherList, AddedAt = DateTime.UtcNow }
            };

            // Add entities to context
            context.Categories.Add(dairyCategory);
            context.SubCategories.Add(milkSubCategory);
            context.ShoppingLists.AddRange(userList, otherList);
            context.Items.AddRange(items);
            context.SaveChanges();
        }

        // Helper to setup Controller with context and user identity
        private ItemsController SetupController(NimblistContext context, string? userId)
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

        // New test for verifying items with categories are properly returned
        [Fact]
        public async Task GetItems_ReturnsItemsWithCategoryInfo_WhenItemsHaveCategories()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new NimblistContext(options);
            SeedData(context);
            var controller = SetupController(context, _testUserId);

            // Act
            var result = await controller.GetItems();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var items = Assert.IsAssignableFrom<IEnumerable<ItemWithCategoryDto>>(okResult.Value);
            
            // Verify the item with categories has the category info
            var itemWithCategory = items.FirstOrDefault(i => i.Name == "Milk");
            Assert.NotNull(itemWithCategory);
            Assert.NotNull(itemWithCategory.CategoryId);
            Assert.Equal("Dairy", itemWithCategory.CategoryName);
            Assert.NotNull(itemWithCategory.SubCategoryId);
            Assert.Equal("Milk", itemWithCategory.SubCategoryName);
        }

        // Test that verifies GetItem returns item with category information
        [Fact]
        public async Task GetItem_ReturnsItemWithCategoryInfo_WhenItemHasCategory()
        {
            // Arrange
            var options = CreateNewContextOptions();
            Guid milkItemId;
            using (var context = new NimblistContext(options))
            {
                SeedData(context);
                milkItemId = context.Items.First(i => i.Name == "Milk").Id;
            }

            using (var context = new NimblistContext(options))
            {
                var controller = SetupController(context, _testUserId);

                // Act
                var result = await controller.GetItem(milkItemId);

                // Assert
                var actionResult = Assert.IsType<ActionResult<ItemWithCategoryDto>>(result);
                var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
                var item = Assert.IsType<ItemWithCategoryDto>(okResult.Value);
                
                Assert.Equal(milkItemId, item.Id);
                Assert.Equal("Milk", item.Name);
                Assert.NotNull(item.CategoryId);
                Assert.Equal("Dairy", item.CategoryName);
                Assert.NotNull(item.SubCategoryId);
                Assert.Equal("Milk", item.SubCategoryName);
            }
        }

        // Test that category info is updated when item is updated
        [Fact]
        public async Task PutItem_UpdatesCategoryInfo_WhenSuccessful()
        {
            // Arrange
            var options = CreateNewContextOptions();
            Guid milkItemId;
            Guid dairyCategoryId;
            using (var context = new NimblistContext(options))
            {
                SeedData(context);
                var milkItem = context.Items.Include(i => i.Category).First(i => i.Name == "Milk");
                milkItemId = milkItem.Id;
                dairyCategoryId = milkItem.CategoryId!.Value;
            }

            using (var context = new NimblistContext(options))
            {
                var controller = SetupController(context, _testUserId);
                  // Create update DTO that changes category-related info
                var updateDto = new ItemUpdateDto 
                { 
                    Name = "Updated Milk", 
                    Quantity = "2 gallons", 
                    IsChecked = true, 
                    ShoppingListId = _testListId
                    // Note: CategoryId and SubCategoryId aren't in the DTO
                };

                // Act
                var result = await controller.PutItem(milkItemId, updateDto);

                // Assert
                Assert.IsType<NoContentResult>(result);

                // Verify database changes
                var updatedItem = await context.Items
                    .Include(i => i.Category)
                    .Include(i => i.SubCategory)
                    .FirstOrDefaultAsync(i => i.Id == milkItemId);
                
                Assert.NotNull(updatedItem);
                Assert.Equal("Updated Milk", updatedItem.Name);
                Assert.Equal("2 gallons", updatedItem.Quantity);
                Assert.True(updatedItem.IsChecked);
                Assert.Null(updatedItem.CategoryId); // Category should be removed
                Assert.Null(updatedItem.SubCategoryId); // Subcategory should be removed
            }
        }

        // Test that covers attempt to update an item to a non-existent shopping list
        [Fact]
        public async Task PutItem_FailsWithException_WhenUpdatingToNonExistentList()
        {
            // Arrange
            var options = CreateNewContextOptions();
            Guid existingItemId;
            using (var context = new NimblistContext(options))
            {
                SeedData(context);
                existingItemId = context.Items.First(i => i.Name == "Bread").Id;
            }

            using (var context = new NimblistContext(options))
            {
                var controller = SetupController(context, _testUserId);
                var nonExistentListId = Guid.NewGuid();
                var updateDto = new ItemUpdateDto
                {
                    Name = "Updated Bread",
                    Quantity = "2 loaves",
                    IsChecked = true,
                    ShoppingListId = nonExistentListId // Non-existent list ID
                };                // Act & Assert
                var updateAction = async () => await controller.PutItem(existingItemId, updateDto);
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(updateAction);
                Assert.Contains("The required data for completing this operation was not found", exception.Message);
            }
        }

        // Test that covers the SignalR notification when item is deleted
        [Fact]
        public async Task DeleteItem_SendsCorrectSignalRNotification()
        {
            // Arrange
            var options = CreateNewContextOptions();
            Guid itemToDeleteId;
            using (var context = new NimblistContext(options))
            {
                SeedData(context);
                itemToDeleteId = context.Items.First(i => i.Name == "Eggs").Id;
            }

            using (var context = new NimblistContext(options))
            {
                var controller = SetupController(context, _testUserId);
                string expectedGroupName = $"list_{_testListId}";

                // Act
                var result = await controller.DeleteItem(itemToDeleteId);

                // Assert
                Assert.IsType<NoContentResult>(result);

                // Verify SignalR message had the correct item ID
                _mockClientProxy.Verify(
                    x => x.SendCoreAsync(
                        "ReceiveItemDeleted", 
                        It.Is<object[]>(o => o != null && o.Length == 1 && o[0] is Guid && (Guid)o[0] == itemToDeleteId),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
                
                // Verify correct group was used
                _mockClients.Verify(c => c.Group(expectedGroupName), Times.Once);
            }
        }

        // Test for ConvertToItemDto helper method
        [Fact]
        public async Task ConvertToItemDto_CorrectlyMapsProperties()
        {
            // Arrange
            var options = CreateNewContextOptions();
            Item itemWithCategory;
            Item itemWithoutCategory;
            using (var context = new NimblistContext(options))
            {
                SeedData(context);
                itemWithCategory = await context.Items
                    .Include(i => i.Category)
                    .Include(i => i.SubCategory)
                    .Include(i => i.List)
                    .FirstAsync(i => i.Name == "Milk");
                
                itemWithoutCategory = await context.Items
                    .Include(i => i.List)
                    .FirstAsync(i => i.Name == "Bread");
            }

            using (var context = new NimblistContext(options))
            {
                var controller = SetupController(context, _testUserId);
                
                // Create a private method accessor using reflection
                var methodInfo = typeof(ItemsController)
                    .GetMethod("ConvertToItemDto", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                // Act
                var dtoWithCategory = methodInfo!.Invoke(controller, new object[] { itemWithCategory }) as ItemWithCategoryDto;
                var dtoWithoutCategory = methodInfo!.Invoke(controller, new object[] { itemWithoutCategory }) as ItemWithCategoryDto;

                // Assert
                Assert.NotNull(dtoWithCategory);
                Assert.Equal(itemWithCategory.Id, dtoWithCategory.Id);
                Assert.Equal(itemWithCategory.Name, dtoWithCategory.Name);
                Assert.Equal(itemWithCategory.Quantity, dtoWithCategory.Quantity);
                Assert.Equal(itemWithCategory.IsChecked, dtoWithCategory.IsChecked);
                Assert.Equal(itemWithCategory.ShoppingListId, dtoWithCategory.ShoppingListId);
                Assert.Equal(itemWithCategory.CategoryId, dtoWithCategory.CategoryId);
                Assert.Equal("Dairy", dtoWithCategory.CategoryName);
                Assert.Equal(itemWithCategory.SubCategoryId, dtoWithCategory.SubCategoryId);
                Assert.Equal("Milk", dtoWithCategory.SubCategoryName);
                
                Assert.NotNull(dtoWithoutCategory);
                Assert.Equal(itemWithoutCategory.Id, dtoWithoutCategory.Id);
                Assert.Null(dtoWithoutCategory.CategoryId);
                Assert.Null(dtoWithoutCategory.CategoryName);
                Assert.Null(dtoWithoutCategory.SubCategoryId);
                Assert.Null(dtoWithoutCategory.SubCategoryName);
            }
        }

        public void Dispose()
        {
            // Cleanup if necessary
        }
    }
}
