using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.Controllers;
using Nimblist.Data;
using Nimblist.Data.Models;
using Xunit;

namespace Nimblist.test.Controllers
{
    public class SubCategoriesControllerTests
    {
        private readonly DbContextOptions<NimblistContext> _dbOptions;
        private const string TestUserId = "test-user-id";

        public SubCategoriesControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        private async Task SeedDataAsync(params object[] entities)
        {
            using (var context = new NimblistContext(_dbOptions))
            {
                context.AddRange(entities);
                await context.SaveChangesAsync();
            }
        }

        private SubCategoriesController CreateControllerWithContext(NimblistContext context)
        {
            var controller = new SubCategoriesController(context);

            // Setup the User claims principal for authorization
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
        public async Task GetSubCategories_ReturnsAllSubCategories_WhenNoFilterProvided()
        {
            // Arrange
            var category1 = new Category { Id = Guid.NewGuid(), Name = "Groceries" };
            var category2 = new Category { Id = Guid.NewGuid(), Name = "Electronics" };
            
            var subCategory1 = new SubCategory { Id = Guid.NewGuid(), Name = "Dairy", ParentCategoryId = category1.Id, ParentCategory = category1 };
            var subCategory2 = new SubCategory { Id = Guid.NewGuid(), Name = "Fruits", ParentCategoryId = category1.Id, ParentCategory = category1 };
            var subCategory3 = new SubCategory { Id = Guid.NewGuid(), Name = "Computers", ParentCategoryId = category2.Id, ParentCategory = category2 };
            
            await SeedDataAsync(category1, category2, subCategory1, subCategory2, subCategory3);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.GetSubCategories(null);

                // Assert
                var actionResult = Assert.IsType<ActionResult<IEnumerable<SubCategory>>>(result);
                var returnValue = Assert.IsType<List<SubCategory>>(actionResult.Value);
                Assert.Equal(3, returnValue.Count);
                Assert.Contains(returnValue, sc => sc.Name == "Dairy");
                Assert.Contains(returnValue, sc => sc.Name == "Fruits");
                Assert.Contains(returnValue, sc => sc.Name == "Computers");
                // Verify ordered by name
                Assert.Equal("Computers", returnValue[0].Name);
                Assert.Equal("Dairy", returnValue[1].Name);
                Assert.Equal("Fruits", returnValue[2].Name);
            }
        }

        [Fact]
        public async Task GetSubCategories_ReturnsFilteredSubCategories_WhenParentCategoryIdProvided()
        {
            // Arrange
            var category1 = new Category { Id = Guid.NewGuid(), Name = "Groceries" };
            var category2 = new Category { Id = Guid.NewGuid(), Name = "Electronics" };
            
            var subCategory1 = new SubCategory { Id = Guid.NewGuid(), Name = "Dairy", ParentCategoryId = category1.Id, ParentCategory = category1 };
            var subCategory2 = new SubCategory { Id = Guid.NewGuid(), Name = "Fruits", ParentCategoryId = category1.Id, ParentCategory = category1 };
            var subCategory3 = new SubCategory { Id = Guid.NewGuid(), Name = "Computers", ParentCategoryId = category2.Id, ParentCategory = category2 };
            
            await SeedDataAsync(category1, category2, subCategory1, subCategory2, subCategory3);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.GetSubCategories(category1.Id);

                // Assert
                var actionResult = Assert.IsType<ActionResult<IEnumerable<SubCategory>>>(result);
                var returnValue = Assert.IsType<List<SubCategory>>(actionResult.Value);
                Assert.Equal(2, returnValue.Count);
                Assert.Contains(returnValue, sc => sc.Name == "Dairy");
                Assert.Contains(returnValue, sc => sc.Name == "Fruits");
                Assert.DoesNotContain(returnValue, sc => sc.Name == "Computers");
                // Verify ordered by name
                Assert.Equal("Dairy", returnValue[0].Name);
                Assert.Equal("Fruits", returnValue[1].Name);
            }
        }

        [Fact]
        public async Task GetSubCategories_ReturnsEmptyList_WhenNoSubCategoriesExist()
        {
            // Arrange
            var category1 = new Category { Id = Guid.NewGuid(), Name = "Empty Category" };
            await SeedDataAsync(category1);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.GetSubCategories(category1.Id);

                // Assert
                var actionResult = Assert.IsType<ActionResult<IEnumerable<SubCategory>>>(result);
                var returnValue = Assert.IsType<List<SubCategory>>(actionResult.Value);
                Assert.Empty(returnValue);
            }
        }

        [Fact]
        public async Task GetSubCategories_ReturnsEmptyList_WhenInvalidParentCategoryId()
        {
            // Arrange
            var category1 = new Category { Id = Guid.NewGuid(), Name = "Groceries" };
            var subCategory1 = new SubCategory { Id = Guid.NewGuid(), Name = "Dairy", ParentCategoryId = category1.Id, ParentCategory = category1 };
            
            await SeedDataAsync(category1, subCategory1);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);
                var nonExistentCategoryId = Guid.NewGuid();

                // Act
                var result = await controller.GetSubCategories(nonExistentCategoryId);

                // Assert
                var actionResult = Assert.IsType<ActionResult<IEnumerable<SubCategory>>>(result);
                var returnValue = Assert.IsType<List<SubCategory>>(actionResult.Value);
                Assert.Empty(returnValue);
            }
        }

        [Fact]
        public async Task GetSubCategory_ReturnsSubCategory_WhenIdExists()
        {
            // Arrange
            var category1 = new Category { Id = Guid.NewGuid(), Name = "Groceries" };
            var subCategory1 = new SubCategory { Id = Guid.NewGuid(), Name = "Dairy", ParentCategoryId = category1.Id, ParentCategory = category1 };
            
            await SeedDataAsync(category1, subCategory1);

            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);

                // Act
                var result = await controller.GetSubCategory(subCategory1.Id);

                // Assert
                var actionResult = Assert.IsType<ActionResult<SubCategory>>(result);
                var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
                var returnValue = Assert.IsType<SubCategory>(okResult.Value);
                Assert.Equal(subCategory1.Id, returnValue.Id);
                Assert.Equal("Dairy", returnValue.Name);
                Assert.Equal(category1.Id, returnValue.ParentCategoryId);
                // Verify the parent category is included
                Assert.NotNull(returnValue.ParentCategory);
                Assert.Equal("Groceries", returnValue.ParentCategory.Name);
            }
        }

        [Fact]
        public async Task GetSubCategory_ReturnsNotFound_WhenIdDoesNotExist()
        {
            // Arrange
            using (var context = new NimblistContext(_dbOptions))
            {
                var controller = CreateControllerWithContext(context);
                var nonExistentId = Guid.NewGuid();

                // Act
                var result = await controller.GetSubCategory(nonExistentId);

                // Assert
                var actionResult = Assert.IsType<ActionResult<SubCategory>>(result);
                Assert.IsType<NotFoundResult>(actionResult.Result);
            }
        }
    }
}
