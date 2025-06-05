using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.Controllers; // Your controller namespace
using Nimblist.Data;             // Your DbContext namespace
using Nimblist.Data.Models;      // Your models namespace
using Xunit;

namespace Nimblist.test.Controllers
{
    public class CategoriesControllerTests
    {
        private readonly DbContextOptions<NimblistContext> _dbOptions;
        private const string TestUserId = "test-user-id"; // Example User ID

        public CategoriesControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB for each test run
                .Options;
        }

        // Helper to seed data
        private async Task SeedDataAsync(params object[] entities)
        {
            using (var context = new NimblistContext(_dbOptions))
            {
                context.AddRange(entities);
                await context.SaveChangesAsync();
            }
        }

        // Helper to create controller with context and user
        private CategoriesController CreateControllerWithContext(NimblistContext context)
        {
            var controller = new CategoriesController(context);

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

        // --- Tests for GetCategories ---

        [Fact]
        public async Task GetCategories_ReturnsOkResult_WithListOfCategoriesAndSubcategories()
        {
            // Arrange
            var category1Id = Guid.NewGuid();
            var category2Id = Guid.NewGuid();
            await SeedDataAsync(
                new Category { Id = category1Id, Name = "Bakery", SubCategories = new List<SubCategory> { new SubCategory { Id = Guid.NewGuid(), Name = "Bread" } } },
                new Category { Id = category2Id, Name = "Dairy", SubCategories = new List<SubCategory> { new SubCategory { Id = Guid.NewGuid(), Name = "Milk" } } }
            );

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.GetCategories();

                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var returnValue = Assert.IsAssignableFrom<IEnumerable<Category>>(okResult.Value);
                Assert.Equal(2, returnValue.Count());
                Assert.Equal("Bakery", returnValue.First().Name); // Assuming order by name "Bakery" then "Dairy"
                Assert.True(returnValue.First().SubCategories.Any(sc => sc.Name == "Bread"));
                Assert.Equal("Dairy", returnValue.Last().Name);
                Assert.True(returnValue.Last().SubCategories.Any(sc => sc.Name == "Milk"));
            }
        }

        [Fact]
        public async Task GetCategories_ReturnsCategoriesOrderedByName()
        {
            // Arrange
            await SeedDataAsync(
                new Category { Id = Guid.NewGuid(), Name = "Vegetables" },
                new Category { Id = Guid.NewGuid(), Name = "Fruits" },
                new Category { Id = Guid.NewGuid(), Name = "Meat" }
            );

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.GetCategories();

                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var returnValue = Assert.IsAssignableFrom<List<Category>>(okResult.Value); // To check order easily
                Assert.Equal(3, returnValue.Count());
                Assert.Equal("Fruits", returnValue[0].Name);
                Assert.Equal("Meat", returnValue[1].Name);
                Assert.Equal("Vegetables", returnValue[2].Name);
            }
        }

        [Fact]
        public async Task GetCategories_ReturnsOkResult_WithEmptyList_WhenNoCategoriesExist()
        {
            // Arrange
            // No categories seeded

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.GetCategories();

                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var returnValue = Assert.IsAssignableFrom<IEnumerable<Category>>(okResult.Value);
                Assert.Empty(returnValue);
            }
        }

        // --- Tests for GetCategory(Guid id) ---

        [Fact]
        public async Task GetCategory_ReturnsOkResult_WithCategoryAndSubcategories_WhenCategoryExists()
        {
            // Arrange
            var categoryId = Guid.NewGuid();
            var subCategoryId = Guid.NewGuid();
            await SeedDataAsync(
                new Category { Id = categoryId, Name = "Snacks", SubCategories = new List<SubCategory> { new SubCategory { Id = subCategoryId, Name = "Crisps",  ParentCategoryId = categoryId } } }
            );

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.GetCategory(categoryId);

                // Assert
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var returnValue = Assert.IsType<Category>(okResult.Value);
                Assert.Equal(categoryId, returnValue.Id);
                Assert.Equal("Snacks", returnValue.Name);
                Assert.NotNull(returnValue.SubCategories);
                Assert.Single(returnValue.SubCategories);
                Assert.Equal("Crisps", returnValue.SubCategories.First().Name);
            }
        }

        [Fact]
        public async Task GetCategory_ReturnsNotFound_WhenCategoryDoesNotExist()
        {
            // Arrange
            var nonExistentCategoryId = Guid.NewGuid();
            // Seed some other category to make sure it's not an empty DB issue
            await SeedDataAsync(new Category { Id = Guid.NewGuid(), Name = "Beverages" });


            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.GetCategory(nonExistentCategoryId);

                // Assert
                Assert.IsType<NotFoundResult>(result.Result);
            }
        }
    }
}