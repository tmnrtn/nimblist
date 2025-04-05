using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Nimblist.api.Controllers;
using Nimblist.api.DTO;
using Nimblist.Data;
using Nimblist.Data.Models;
using Xunit;

namespace Nimblist.Tests
{
    public class ShoppingListsControllerTests
    {
        private readonly Mock<NimblistContext> _mockContext;
        private readonly ShoppingListsController _controller;
        private readonly Mock<DbSet<ShoppingList>> _mockSet;

        public ShoppingListsControllerTests()
        {
            _mockContext = new Mock<NimblistContext>(new DbContextOptions<NimblistContext>());
            _mockSet = new Mock<DbSet<ShoppingList>>();
            _controller = new ShoppingListsController(_mockContext.Object);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user-id")
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        [Fact]
        public async Task GetShoppingLists_ReturnsOkResult_WithUserShoppingLists()
        {
            // Arrange
            var shoppingLists = new List<ShoppingList>
            {
                new ShoppingList { Id = Guid.NewGuid(), Name = "Test List", UserId = "test-user-id" }
            }.AsQueryable();

            _mockSet.As<IQueryable<ShoppingList>>().Setup(m => m.Provider).Returns(shoppingLists.Provider);
            _mockSet.As<IQueryable<ShoppingList>>().Setup(m => m.Expression).Returns(shoppingLists.Expression);
            _mockSet.As<IQueryable<ShoppingList>>().Setup(m => m.ElementType).Returns(shoppingLists.ElementType);
            _mockSet.As<IQueryable<ShoppingList>>().Setup(m => m.GetEnumerator()).Returns(shoppingLists.GetEnumerator());

            _mockContext.Setup(c => c.ShoppingLists).Returns(_mockSet.Object);

            // Act
            var result = await _controller.GetShoppingLists();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<List<ShoppingList>>(okResult.Value);
            Assert.Single(returnValue);
        }

        [Fact]
        public async Task GetShoppingList_ReturnsNotFound_WhenListDoesNotExist()
        {
            // Arrange
            var listId = Guid.NewGuid();
            _mockSet.Setup(m => m.FindAsync(listId)).ReturnsAsync((ShoppingList)null);
            _mockContext.Setup(c => c.ShoppingLists).Returns(_mockSet.Object);

            // Act
            var result = await _controller.GetShoppingList(listId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task PostShoppingList_ReturnsCreatedAtActionResult()
        {
            // Arrange
            var newList = new ShoppingListInputDto { Name = "New List" };

            // Act
            var result = await _controller.PostShoppingList(newList);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnValue = Assert.IsType<ShoppingList>(createdAtActionResult.Value);
            Assert.Equal(newList.Name, returnValue.Name);
        }

        [Fact]
        public async Task DeleteShoppingList_ReturnsNoContent_WhenListIsDeleted()
        {
            // Arrange
            var listId = Guid.NewGuid();
            var shoppingList = new ShoppingList { Id = listId, UserId = "test-user-id" };

            _mockSet.Setup(m => m.FindAsync(listId)).ReturnsAsync(shoppingList);
            _mockContext.Setup(c => c.ShoppingLists).Returns(_mockSet.Object);

            // Act
            var result = await _controller.DeleteShoppingList(listId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }
    }
}
