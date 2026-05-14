using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.Data;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Nimblist.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ImageSearchController : ControllerBase
    {
        private readonly NimblistContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public ImageSearchController(NimblistContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        // GET /api/imagesearch?q=chocolate+cake
        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(new { error = "Query parameter 'q' is required." });

            var settings = await _context.LlmSettings.FirstOrDefaultAsync();
            if (settings == null || string.IsNullOrEmpty(settings.ImageSearchApiKey))
            {
                return StatusCode(503, new { error = "Image search is not configured. Add a Brave Search API key in Admin → LLM / Search Settings." });
            }

            var url = $"https://api.search.brave.com/res/v1/images/search"
                    + $"?q={Uri.EscapeDataString(q)}"
                    + $"&count=10"
                    + $"&safesearch=moderate";

            try
            {
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("Accept-Encoding", "gzip");
                request.Headers.Add("X-Subscription-Token", settings.ImageSearchApiKey);

                var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, new { error = $"Brave Search API returned {(int)response.StatusCode}: {errorBody}" });
                }

                var braveResponse = await response.Content.ReadFromJsonAsync<BraveSearchResponse>();
                var results = (braveResponse?.Results ?? [])
                    .Where(item => !string.IsNullOrEmpty(item.Properties?.Url))
                    .Select(item => new ImageSearchResultDto
                    {
                        Title = item.Title,
                        ImageUrl = item.Properties!.Url!,
                        ThumbnailUrl = item.Thumbnail?.Src,
                        SourceUrl = item.Url,
                    });

                return Ok(results);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(502, new { error = $"Failed to reach Brave Search API: {ex.Message}" });
            }
        }

        // ── Response DTOs ────────────────────────────────────────────────────

        private record ImageSearchResultDto
        {
            public string? Title { get; init; }
            public required string ImageUrl { get; init; }
            public string? ThumbnailUrl { get; init; }
            public string? SourceUrl { get; init; }
        }

        private record BraveSearchResponse
        {
            [JsonPropertyName("results")]
            public List<BraveImageResult>? Results { get; init; }
        }

        private record BraveImageResult
        {
            [JsonPropertyName("title")]
            public string? Title { get; init; }

            [JsonPropertyName("url")]
            public string? Url { get; init; }

            [JsonPropertyName("thumbnail")]
            public BraveThumbnail? Thumbnail { get; init; }

            [JsonPropertyName("properties")]
            public BraveImageProperties? Properties { get; init; }
        }

        private record BraveThumbnail
        {
            [JsonPropertyName("src")]
            public string? Src { get; init; }
        }

        private record BraveImageProperties
        {
            [JsonPropertyName("url")]
            public string? Url { get; init; }
        }
    }
}
