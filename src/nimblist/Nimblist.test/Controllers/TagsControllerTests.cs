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
    public class TagsControllerTests
    {
        private readonly DbContextOptions<NimblistContext> _dbOptions;
        private const string TestUserId = "test-user-id";
        private const string OtherUserId = "other-user-id";

        public TagsControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        private async Task SeedDataAsync(params object[] entities)
        {
            using var context = new NimblistContext(_dbOptions);
            context.AddRange(entities);
            await context.SaveChangesAsync();
        }

        private TagsController CreateControllerWithContext(NimblistContext context, string userId = TestUserId)
        {
            var controller = new TagsController(context);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            }, "mock"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
            return controller;
        }

        private TagsController CreateUnauthenticatedController(NimblistContext context)
        {
            var controller = new TagsController(context);

            // ClaimsIdentity with no authenticationType = not authenticated; no NameIdentifier claim
            var user = new ClaimsPrincipal(new ClaimsIdentity());

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
            return controller;
        }

        // --- GetTags ---

        [Fact]
        public async Task GetTags_ReturnsOwnTagsOnly()
        {
            // Arrange: 2 tags for test user, 1 for other user
            await SeedDataAsync(
                new Tag { Id = Guid.NewGuid(), Name = "Dinner", Color = "red",   UserId = TestUserId },
                new Tag { Id = Guid.NewGuid(), Name = "Breakfast", Color = null,  UserId = TestUserId },
                new Tag { Id = Guid.NewGuid(), Name = "Lunch",     Color = "blue", UserId = OtherUserId }
            );

            using var context = new NimblistContext(_dbOptions);
            var controller = CreateControllerWithContext(context);

            // Act
            var result = await controller.GetTags();

            // Assert: only 2 tags returned, ordered alphabetically
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var tags = Assert.IsAssignableFrom<List<TagDto>>(ok.Value);
            Assert.Equal(2, tags.Count);
            Assert.Equal("Breakfast", tags[0].Name);
            Assert.Equal("Dinner",    tags[1].Name);
        }

        [Fact]
        public async Task GetTags_Unauthenticated_ReturnsUnauthorized()
        {
            using var context = new NimblistContext(_dbOptions);
            var controller = CreateUnauthenticatedController(context);

            var result = await controller.GetTags();

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        // --- CreateTag ---

        [Fact]
        public async Task CreateTag_ValidInput_ReturnsCreated()
        {
            using var context = new NimblistContext(_dbOptions);
            var controller = CreateControllerWithContext(context);
            var dto = new TagInputDto("Snacks", "green");

            var result = await controller.CreateTag(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var tagDto = Assert.IsType<TagDto>(created.Value);
            Assert.Equal("Snacks", tagDto.Name);
            Assert.Equal("green",  tagDto.Color);

            // Verify persisted to DB
            using var verifyCtx = new NimblistContext(_dbOptions);
            var saved = await verifyCtx.Tags.SingleAsync(t => t.UserId == TestUserId);
            Assert.Equal("Snacks", saved.Name);
        }

        [Fact]
        public async Task CreateTag_EmptyName_ReturnsBadRequest()
        {
            using var context = new NimblistContext(_dbOptions);
            var controller = CreateControllerWithContext(context);
            var dto = new TagInputDto("   ", null);

            var result = await controller.CreateTag(dto);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateTag_DuplicateName_ReturnsConflict()
        {
            await SeedDataAsync(
                new Tag { Id = Guid.NewGuid(), Name = "Healthy", Color = null, UserId = TestUserId }
            );

            using var context = new NimblistContext(_dbOptions);
            var controller = CreateControllerWithContext(context);
            var dto = new TagInputDto("Healthy", "blue");

            var result = await controller.CreateTag(dto);

            Assert.IsType<ConflictObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateTag_DuplicateNameOtherUser_ReturnsCreated()
        {
            // Other user already has a tag called "Healthy"; test user should be able to create one too
            await SeedDataAsync(
                new Tag { Id = Guid.NewGuid(), Name = "Healthy", Color = null, UserId = OtherUserId }
            );

            using var context = new NimblistContext(_dbOptions);
            var controller = CreateControllerWithContext(context);
            var dto = new TagInputDto("Healthy", null);

            var result = await controller.CreateTag(dto);

            Assert.IsType<CreatedAtActionResult>(result.Result);
        }

        // --- UpdateTag ---

        [Fact]
        public async Task UpdateTag_ValidInput_ReturnsOkWithUpdatedValues()
        {
            var tagId = Guid.NewGuid();
            await SeedDataAsync(
                new Tag { Id = tagId, Name = "OldName", Color = "red", UserId = TestUserId }
            );

            using var context = new NimblistContext(_dbOptions);
            var controller = CreateControllerWithContext(context);
            var dto = new TagInputDto("NewName", "blue");

            var result = await controller.UpdateTag(tagId, dto);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var tagDto = Assert.IsType<TagDto>(ok.Value);
            Assert.Equal("NewName", tagDto.Name);
            Assert.Equal("blue",    tagDto.Color);
        }

        [Fact]
        public async Task UpdateTag_NotFound_ReturnsNotFound()
        {
            // Tag belongs to other user, so test user cannot update it
            var tagId = Guid.NewGuid();
            await SeedDataAsync(
                new Tag { Id = tagId, Name = "Foreign", Color = null, UserId = OtherUserId }
            );

            using var context = new NimblistContext(_dbOptions);
            var controller = CreateControllerWithContext(context);
            var dto = new TagInputDto("Updated", null);

            var result = await controller.UpdateTag(tagId, dto);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task UpdateTag_EmptyName_ReturnsBadRequest()
        {
            var tagId = Guid.NewGuid();
            await SeedDataAsync(
                new Tag { Id = tagId, Name = "Valid", Color = null, UserId = TestUserId }
            );

            using var context = new NimblistContext(_dbOptions);
            var controller = CreateControllerWithContext(context);
            var dto = new TagInputDto("  ", null);

            var result = await controller.UpdateTag(tagId, dto);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task UpdateTag_DuplicateName_ReturnsConflict()
        {
            var tagId  = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            await SeedDataAsync(
                new Tag { Id = tagId,   Name = "Tag A", Color = null, UserId = TestUserId },
                new Tag { Id = otherId, Name = "Tag B", Color = null, UserId = TestUserId }
            );

            using var context = new NimblistContext(_dbOptions);
            var controller = CreateControllerWithContext(context);
            // Trying to rename "Tag A" to "Tag B" which already exists for the same user
            var dto = new TagInputDto("Tag B", null);

            var result = await controller.UpdateTag(tagId, dto);

            Assert.IsType<ConflictObjectResult>(result.Result);
        }

        [Fact]
        public async Task UpdateTag_SameName_ReturnsOk()
        {
            // Renaming a tag to its own existing name should succeed (duplicate check excludes the tag's own id)
            var tagId = Guid.NewGuid();
            await SeedDataAsync(
                new Tag { Id = tagId, Name = "Veggies", Color = "green", UserId = TestUserId }
            );

            using var context = new NimblistContext(_dbOptions);
            var controller = CreateControllerWithContext(context);
            var dto = new TagInputDto("Veggies", "green");

            var result = await controller.UpdateTag(tagId, dto);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var tagDto = Assert.IsType<TagDto>(ok.Value);
            Assert.Equal("Veggies", tagDto.Name);
        }

        // --- DeleteTag ---

        [Fact]
        public async Task DeleteTag_ExistingOwned_ReturnsNoContent_AndRemovesFromDb()
        {
            var tagId = Guid.NewGuid();
            await SeedDataAsync(
                new Tag { Id = tagId, Name = "ToDelete", Color = null, UserId = TestUserId }
            );

            using var context = new NimblistContext(_dbOptions);
            var controller = CreateControllerWithContext(context);

            var result = await controller.DeleteTag(tagId);

            Assert.IsType<NoContentResult>(result);

            // Verify removed from DB
            using var verifyCtx = new NimblistContext(_dbOptions);
            var deleted = await verifyCtx.Tags.FindAsync(tagId);
            Assert.Null(deleted);
        }

        [Fact]
        public async Task DeleteTag_NotFound_ReturnsNotFound()
        {
            // Tag belongs to another user
            var tagId = Guid.NewGuid();
            await SeedDataAsync(
                new Tag { Id = tagId, Name = "Foreign", Color = null, UserId = OtherUserId }
            );

            using var context = new NimblistContext(_dbOptions);
            var controller = CreateControllerWithContext(context);

            var result = await controller.DeleteTag(tagId);

            Assert.IsType<NotFoundResult>(result);
        }
    }
}
