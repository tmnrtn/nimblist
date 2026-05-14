using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.Protected;
using Nimblist.api.Controllers;
using Nimblist.Data;
using Nimblist.Data.Models;
using Xunit;

namespace Nimblist.test.Controllers
{
    public class ImageSearchControllerTests
    {
        private const string TestUserId = "test-user-id";
        private const string ValidApiKey = "brave-api-key-abc";

        private static DbContextOptions<NimblistContext> NewDb() =>
            new DbContextOptionsBuilder<NimblistContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        private static async Task SeedAsync(DbContextOptions<NimblistContext> opts, params object[] entities)
        {
            using var ctx = new NimblistContext(opts);
            ctx.AddRange(entities);
            await ctx.SaveChangesAsync();
        }

        /// <summary>
        /// Builds an IHttpClientFactory whose single HttpClient always returns
        /// <paramref name="statusCode"/> with a JSON-serialised <paramref name="content"/> body.
        /// Pass <c>null</c> for content to use an empty body.
        /// </summary>
        private static IHttpClientFactory BuildHttpFactory(HttpStatusCode statusCode, object? content)
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = content != null
                        ? JsonContent.Create(content)
                        : new StringContent(string.Empty)
                });

            var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://test/") };
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
            return factory.Object;
        }

        /// <summary>
        /// Builds an IHttpClientFactory that throws HttpRequestException on every request.
        /// </summary>
        private static IHttpClientFactory BuildThrowingHttpFactory()
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Connection refused"));

            var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://test/") };
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
            return factory.Object;
        }

        private static ImageSearchController CreateController(
            NimblistContext context,
            IHttpClientFactory httpFactory)
        {
            var controller = new ImageSearchController(context, httpFactory);
            var user = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, TestUserId) },
                "mockAuth"));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
            return controller;
        }

        /// <summary>
        /// Builds a minimal Brave Search API JSON response payload.
        /// Each entry is { title, url, properties: { url }, thumbnail: { src } }.
        /// Pass null url/thumbnailSrc to simulate missing fields.
        /// </summary>
        private static object BuildBraveResponse(IEnumerable<(string? imageUrl, string? thumbSrc, string? title, string? sourceUrl)> items)
        {
            var results = items.Select(i => new
            {
                title = i.title,
                url = i.sourceUrl,
                properties = i.imageUrl != null ? new { url = i.imageUrl } : (object?)null,
                thumbnail = i.thumbSrc != null ? new { src = i.thumbSrc } : (object?)null,
            });
            return new { results };
        }

        // ── Tests ────────────────────────────────────────────────────────────

        [Fact]
        public async Task Search_ReturnsServiceUnavailable_WhenApiKeyNotConfigured()
        {
            // No LlmSettings row at all
            var opts = NewDb();
            var factory = BuildHttpFactory(HttpStatusCode.OK, null);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, factory).Search("chocolate cake");

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(503, obj.StatusCode);
        }

        [Fact]
        public async Task Search_ReturnsServiceUnavailable_WhenImageSearchApiKeyIsEmpty()
        {
            var opts = NewDb();
            await SeedAsync(opts, new LlmSettings
            {
                Id = 1,
                ImageSearchApiKey = "" // present but empty
            });

            var factory = BuildHttpFactory(HttpStatusCode.OK, null);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, factory).Search("chocolate cake");

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(503, obj.StatusCode);
        }

        [Fact]
        public async Task Search_ReturnsResults_WhenBraveApiSucceeds()
        {
            var opts = NewDb();
            await SeedAsync(opts, new LlmSettings { Id = 1, ImageSearchApiKey = ValidApiKey });

            var bravePayload = BuildBraveResponse(new[]
            {
                ("https://example.com/img1.jpg", "https://example.com/thumb1.jpg", "Image One", "https://example.com/page1"),
                ("https://example.com/img2.jpg", "https://example.com/thumb2.jpg", "Image Two", "https://example.com/page2"),
            });
            var factory = BuildHttpFactory(HttpStatusCode.OK, bravePayload);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, factory).Search("cake");

            var ok = Assert.IsType<OkObjectResult>(result);
            // Deserialise via JSON round-trip since the DTO is private to the controller.
            // Use case-insensitive property lookup to avoid fragility around camelCase/PascalCase.
            var json = JsonSerializer.Serialize(ok.Value);
            var docs = JsonSerializer.Deserialize<List<JsonElement>>(json)!;
            Assert.Equal(2, docs.Count);

            // Find property by iterating — avoids PascalCase vs camelCase fragility
            static string? GetStringProp(JsonElement el, string name) =>
                el.EnumerateObject()
                  .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                  .Value.GetString();

            Assert.Equal("https://example.com/img1.jpg",   GetStringProp(docs[0], "imageUrl"));
            Assert.Equal("https://example.com/thumb1.jpg", GetStringProp(docs[0], "thumbnailUrl"));
        }

        [Fact]
        public async Task Search_ReturnsBadGateway_WhenBraveApiReturnsError()
        {
            var opts = NewDb();
            await SeedAsync(opts, new LlmSettings { Id = 1, ImageSearchApiKey = ValidApiKey });

            // Brave returns 429 Too Many Requests
            var factory = BuildHttpFactory(HttpStatusCode.TooManyRequests, new { error = "rate limited" });

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, factory).Search("cake");

            // Controller forwards the upstream status code
            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(429, obj.StatusCode);
        }

        [Fact]
        public async Task Search_ReturnsBadGateway_WhenHttpRequestExceptionThrown()
        {
            var opts = NewDb();
            await SeedAsync(opts, new LlmSettings { Id = 1, ImageSearchApiKey = ValidApiKey });

            var factory = BuildThrowingHttpFactory();

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, factory).Search("cake");

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(502, obj.StatusCode);
        }

        [Fact]
        public async Task Search_FiltersOutResultsWithNullUrl()
        {
            var opts = NewDb();
            await SeedAsync(opts, new LlmSettings { Id = 1, ImageSearchApiKey = ValidApiKey });

            // Mix of items: one has a valid image URL, one has null properties URL
            var bravePayload = BuildBraveResponse(new[]
            {
                ("https://example.com/valid.jpg", "https://example.com/thumb.jpg", "Valid", "https://example.com/page"),
                // null imageUrl means properties.url is null — should be filtered out
                (null, "https://example.com/thumb2.jpg", "No Image URL", "https://example.com/page2"),
            });
            var factory = BuildHttpFactory(HttpStatusCode.OK, bravePayload);

            using var ctx = new NimblistContext(opts);
            var result = await CreateController(ctx, factory).Search("cake");

            var ok = Assert.IsType<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(ok.Value);
            var docs = JsonSerializer.Deserialize<List<JsonElement>>(json)!;
            // Only the result with a non-null imageUrl should survive the Where filter
            Assert.Single(docs);
            static string? GetStringProp(JsonElement el, string name) =>
                el.EnumerateObject()
                  .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                  .Value.GetString();
            Assert.Equal("https://example.com/valid.jpg", GetStringProp(docs[0], "imageUrl"));
        }
    }
}
