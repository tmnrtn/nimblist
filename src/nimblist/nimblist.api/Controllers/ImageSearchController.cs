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
            if (settings == null
                || string.IsNullOrEmpty(settings.GoogleSearchApiKey)
                || string.IsNullOrEmpty(settings.GoogleSearchCseId))
            {
                return StatusCode(503, new { error = "Google Image Search is not configured. Add an API key and CSE ID in Admin → LLM / Search Settings." });
            }

            var url = $"https://www.googleapis.com/customsearch/v1"
                    + $"?key={Uri.EscapeDataString(settings.GoogleSearchApiKey)}"
                    + $"&cx={Uri.EscapeDataString(settings.GoogleSearchCseId)}"
                    + $"&searchType=image"
                    + $"&q={Uri.EscapeDataString(q)}"
                    + $"&num=10"
                    + $"&safe=active";

            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, new { error = $"Google API returned {(int)response.StatusCode}: {errorBody}" });
                }

                var googleResponse = await response.Content.ReadFromJsonAsync<GoogleSearchResponse>();
                var results = (googleResponse?.Items ?? [])
                    .Where(item => !string.IsNullOrEmpty(item.Link))
                    .Select(item => new ImageSearchResultDto
                    {
                        Title = item.Title,
                        ImageUrl = item.Link!,
                        ThumbnailUrl = item.Image?.ThumbnailLink,
                        SourceUrl = item.Image?.ContextLink,
                    });

                return Ok(results);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(502, new { error = $"Failed to reach Google API: {ex.Message}" });
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

        private record GoogleSearchResponse
        {
            [JsonPropertyName("items")]
            public List<GoogleSearchItem>? Items { get; init; }
        }

        private record GoogleSearchItem
        {
            [JsonPropertyName("title")]
            public string? Title { get; init; }

            [JsonPropertyName("link")]
            public string? Link { get; init; }

            [JsonPropertyName("image")]
            public GoogleSearchImageInfo? Image { get; init; }
        }

        private record GoogleSearchImageInfo
        {
            [JsonPropertyName("thumbnailLink")]
            public string? ThumbnailLink { get; init; }

            [JsonPropertyName("contextLink")]
            public string? ContextLink { get; init; }
        }
    }
}
