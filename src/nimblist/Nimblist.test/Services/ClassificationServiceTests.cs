using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nimblist.api.Services;
using Nimblist.Data;
using Nimblist.Data.Models;
using Xunit;

namespace Nimblist.test.Services
{
    /// <summary>
    /// Captures the last request and returns a pre-configured response.
    /// </summary>
    internal class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StubHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }

    public class ClassificationServiceTests
    {
        private readonly DbContextOptions<NimblistContext> _dbOptions;

        public ClassificationServiceTests()
        {
            _dbOptions = new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        private async Task SeedAsync(params object[] entities)
        {
            using var ctx = new NimblistContext(_dbOptions);
            ctx.AddRange(entities);
            await ctx.SaveChangesAsync();
        }

        private IConfiguration BuildConfig(string? predictUrl)
        {
            var dict = new System.Collections.Generic.Dictionary<string, string?>();
            if (predictUrl != null)
                dict["ClassificationService:PredictUrl"] = predictUrl;
            return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        }

        private ClassificationService BuildService(HttpResponseMessage httpResponse, string predictUrl = "http://classification/predict")
        {
            var handler = new StubHttpMessageHandler(httpResponse);
            var client = new HttpClient(handler);

            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient("ClassificationServiceClient")).Returns(client);

            using var ctx = new NimblistContext(_dbOptions);
            return new ClassificationService(
                new NimblistContext(_dbOptions),
                factory.Object,
                BuildConfig(predictUrl),
                NullLogger<ClassificationService>.Instance);
        }

        private static HttpResponseMessage JsonResponse(object body, HttpStatusCode status = HttpStatusCode.OK)
        {
            var json = JsonSerializer.Serialize(body);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        // --- ClassifyAsync: early-exit cases ---

        [Fact]
        public async Task ClassifyAsync_NoPredictUrl_ReturnsNulls()
        {
            var factory = new Mock<IHttpClientFactory>();
            using var ctx = new NimblistContext(_dbOptions);
            var svc = new ClassificationService(ctx, factory.Object, BuildConfig(null), NullLogger<ClassificationService>.Instance);

            var (cat, sub) = await svc.ClassifyAsync("Milk");

            Assert.Null(cat);
            Assert.Null(sub);
            factory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ClassifyAsync_EmptyItemName_ReturnsNulls()
        {
            var factory = new Mock<IHttpClientFactory>();
            using var ctx = new NimblistContext(_dbOptions);
            var svc = new ClassificationService(ctx, factory.Object, BuildConfig("http://x/predict"), NullLogger<ClassificationService>.Instance);

            var (cat, sub) = await svc.ClassifyAsync("   ");

            Assert.Null(cat);
            Assert.Null(sub);
        }

        // --- ClassifyAsync: HTTP failure ---

        [Fact]
        public async Task ClassifyAsync_HttpReturns500_ReturnsNulls()
        {
            var svc = BuildService(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var (cat, sub) = await svc.ClassifyAsync("Milk");

            Assert.Null(cat);
            Assert.Null(sub);
        }

        [Fact]
        public async Task ClassifyAsync_HttpClientThrows_ReturnsNulls()
        {
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Throws(new InvalidOperationException("network down"));
            using var ctx = new NimblistContext(_dbOptions);
            var svc = new ClassificationService(ctx, factory.Object, BuildConfig("http://x/predict"), NullLogger<ClassificationService>.Instance);

            var (cat, sub) = await svc.ClassifyAsync("Milk");

            Assert.Null(cat);
            Assert.Null(sub);
        }

        // --- ClassifyAsync: response parsing ---

        [Fact]
        public async Task ClassifyAsync_UnknownCategory_ReturnsNulls()
        {
            var body = new { predicted_primary_category = "Unknown", predicted_sub_category = "N/A" };
            var svc = BuildService(JsonResponse(body));

            var (cat, sub) = await svc.ClassifyAsync("Widget");

            Assert.Null(cat);
            Assert.Null(sub);
        }

        [Fact]
        public async Task ClassifyAsync_CategoryNotInDb_ReturnsNulls()
        {
            var body = new { predicted_primary_category = "Electronics", predicted_sub_category = "Phones" };
            var svc = BuildService(JsonResponse(body));

            var (cat, sub) = await svc.ClassifyAsync("Phone");

            Assert.Null(cat);
            Assert.Null(sub);
        }

        [Fact]
        public async Task ClassifyAsync_CategoryMatchedNoSubCategory_ReturnsCategoryIdOnly()
        {
            var categoryId = Guid.NewGuid();
            await SeedAsync(new Category { Id = categoryId, Name = "Dairy" });

            var body = new { predicted_primary_category = "Dairy", predicted_sub_category = "Unknown" };
            var svc = BuildService(JsonResponse(body));

            var (cat, sub) = await svc.ClassifyAsync("Cheese");

            Assert.Equal(categoryId, cat);
            Assert.Null(sub);
        }

        [Fact]
        public async Task ClassifyAsync_CategoryAndSubCategoryMatched_ReturnsBothIds()
        {
            var categoryId = Guid.NewGuid();
            var subCategoryId = Guid.NewGuid();
            await SeedAsync(
                new Category { Id = categoryId, Name = "Dairy" },
                new SubCategory { Id = subCategoryId, Name = "Milk", ParentCategoryId = categoryId }
            );

            var body = new { predicted_primary_category = "Dairy", predicted_sub_category = "Milk" };
            var svc = BuildService(JsonResponse(body));

            var (cat, sub) = await svc.ClassifyAsync("Whole Milk");

            Assert.Equal(categoryId, cat);
            Assert.Equal(subCategoryId, sub);
        }

        [Fact]
        public async Task ClassifyAsync_CategoryMatchCaseInsensitive_ReturnsCategory()
        {
            var categoryId = Guid.NewGuid();
            await SeedAsync(new Category { Id = categoryId, Name = "Dairy" });

            var body = new { predicted_primary_category = "DAIRY", predicted_sub_category = "N/A" };
            var svc = BuildService(JsonResponse(body));

            var (cat, sub) = await svc.ClassifyAsync("Butter");

            Assert.Equal(categoryId, cat);
            Assert.Null(sub);
        }

        [Fact]
        public async Task ClassifyAsync_SubCategoryBelongsToDifferentCategory_ReturnsNullSubCategory()
        {
            var categoryId = Guid.NewGuid();
            var otherCategoryId = Guid.NewGuid();
            var subCategoryId = Guid.NewGuid();
            await SeedAsync(
                new Category { Id = categoryId, Name = "Dairy" },
                new Category { Id = otherCategoryId, Name = "Bakery" },
                new SubCategory { Id = subCategoryId, Name = "Bread", ParentCategoryId = otherCategoryId }
            );

            var body = new { predicted_primary_category = "Dairy", predicted_sub_category = "Bread" };
            var svc = BuildService(JsonResponse(body));

            var (cat, sub) = await svc.ClassifyAsync("Item");

            Assert.Equal(categoryId, cat);
            Assert.Null(sub);
        }
    }
}
