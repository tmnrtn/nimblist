using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Nimblist.api.Controllers;
using Nimblist.api.DTO;
using Nimblist.api.Hubs;
using Nimblist.api.Services;
using Nimblist.Data;
using Nimblist.Data.Models;
using Xunit;

namespace Nimblist.test.Controllers
{
    public class ItemsControllerClassificationTests
    {
        private readonly Mock<IHubContext<ShoppingListHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;
        private readonly Mock<IClassificationService> _mockClassificationService;

        private readonly string _testUserId = "test-user-id";
        private readonly Guid _testListId = Guid.NewGuid();

        public ItemsControllerClassificationTests()
        {
            _mockHubContext = new Mock<IHubContext<ShoppingListHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

            _mockClassificationService = new Mock<IClassificationService>();
        }

        private DbContextOptions<NimblistContext> CreateNewContextOptions() =>
            new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        private ItemsController SetupController(NimblistContext context, string userId)
        {
            var controller = new ItemsController(
                context,
                _mockHubContext.Object,
                _mockClassificationService.Object);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
            }, "mock"));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
            return controller;
        }

        private async Task SeedDataAsync(DbContextOptions<NimblistContext> options, params object[] entities)
        {
            using var context = new NimblistContext(options);
            context.AddRange(entities);
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task PostItem_ClassifiesWithCategoryAndSubCategory_WhenServiceReturnsBoth()
        {
            var options = CreateNewContextOptions();
            var categoryId = Guid.NewGuid();
            var subCategoryId = Guid.NewGuid();
            var category = new Category { Id = categoryId, Name = "Dairy" };
            var subCategory = new SubCategory { Id = subCategoryId, Name = "Milk", ParentCategoryId = categoryId };
            var shoppingList = new ShoppingList { Id = _testListId, UserId = _testUserId, Name = "Test List" };
            await SeedDataAsync(options, shoppingList, category, subCategory);

            _mockClassificationService
                .Setup(s => s.ClassifyAsync("Whole Milk"))
                .ReturnsAsync((categoryId, subCategoryId));

            var controller = SetupController(new NimblistContext(options), _testUserId);
            var newItemDto = new ItemInputDto { Name = "Whole Milk", Quantity = "1 gallon", IsChecked = false, ShoppingListId = _testListId };

            var result = await controller.PostItem(newItemDto);

            var createdAt = Assert.IsType<CreatedAtActionResult>(Assert.IsType<ActionResult<ItemWithCategoryDto>>(result).Result);
            var item = Assert.IsType<ItemWithCategoryDto>(createdAt.Value);

            Assert.Equal("Whole Milk", item.Name);
            Assert.Equal("1 gallon", item.Quantity);
            Assert.Equal(categoryId, item.CategoryId);
            Assert.Equal(subCategoryId, item.SubCategoryId);

            _mockClientProxy.Verify(
                x => x.SendCoreAsync("ReceiveItemAdded", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task PostItem_ClassifiesWithCategoryOnly_WhenServiceReturnsNullSubCategory()
        {
            var options = CreateNewContextOptions();
            var categoryId = Guid.NewGuid();
            var category = new Category { Id = categoryId, Name = "Dairy" };
            var shoppingList = new ShoppingList { Id = _testListId, UserId = _testUserId, Name = "Test List" };
            await SeedDataAsync(options, shoppingList, category);

            _mockClassificationService
                .Setup(s => s.ClassifyAsync("Whole Milk"))
                .ReturnsAsync((categoryId, (Guid?)null));

            var controller = SetupController(new NimblistContext(options), _testUserId);
            var newItemDto = new ItemInputDto { Name = "Whole Milk", Quantity = "1 gallon", IsChecked = false, ShoppingListId = _testListId };

            var result = await controller.PostItem(newItemDto);

            var createdAt = Assert.IsType<CreatedAtActionResult>(Assert.IsType<ActionResult<ItemWithCategoryDto>>(result).Result);
            var item = Assert.IsType<ItemWithCategoryDto>(createdAt.Value);

            Assert.Equal("Whole Milk", item.Name);
            Assert.Equal(categoryId, item.CategoryId);
            Assert.Null(item.SubCategoryId);
        }

        [Fact]
        public async Task PostItem_DoesNotClassify_WhenServiceReturnsNull()
        {
            var options = CreateNewContextOptions();
            var shoppingList = new ShoppingList { Id = _testListId, UserId = _testUserId, Name = "Test List" };
            await SeedDataAsync(options, shoppingList);

            _mockClassificationService
                .Setup(s => s.ClassifyAsync(It.IsAny<string>()))
                .ReturnsAsync(((Guid?)null, (Guid?)null));

            var controller = SetupController(new NimblistContext(options), _testUserId);
            var newItemDto = new ItemInputDto { Name = "Whole Milk", Quantity = "1 gallon", IsChecked = false, ShoppingListId = _testListId };

            var result = await controller.PostItem(newItemDto);

            var createdAt = Assert.IsType<CreatedAtActionResult>(Assert.IsType<ActionResult<ItemWithCategoryDto>>(result).Result);
            var item = Assert.IsType<ItemWithCategoryDto>(createdAt.Value);

            Assert.Equal("Whole Milk", item.Name);
            Assert.Null(item.CategoryId);
            Assert.Null(item.SubCategoryId);
        }

        [Fact]
        public async Task PostItem_StillCreatesItem_WhenClassificationServiceThrows()
        {
            var options = CreateNewContextOptions();
            var shoppingList = new ShoppingList { Id = _testListId, UserId = _testUserId, Name = "Test List" };
            await SeedDataAsync(options, shoppingList);

            _mockClassificationService
                .Setup(s => s.ClassifyAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Classification service unavailable"));

            var controller = SetupController(new NimblistContext(options), _testUserId);
            var newItemDto = new ItemInputDto { Name = "Whole Milk", Quantity = "1 gallon", IsChecked = false, ShoppingListId = _testListId };

            // Classification failure should not prevent item creation
            await Assert.ThrowsAsync<Exception>(() => controller.PostItem(newItemDto));
        }
    }
}
