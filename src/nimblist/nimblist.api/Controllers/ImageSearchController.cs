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
                return StatusCode(503, new { error = "Image search is not configured. Add a Bing Search API key in Admin → LLM / Search Settings." });
            }

            var url = $"https://api.bing.microsoft.com/v7.0/images/search"
                    + $"?q={Uri.EscapeDataString(q)}"
                    + $"&count=10"
                    + $"&safeSearch=Moderate"
                    + $"&imageType=Photo";

            try
            {
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Ocp-Apim-Subscription-Key", settings.ImageSearchApiKey);

                var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, new { error = $"Bing API returned {(int)response.StatusCode}: {errorBody}" });
                }

                var bingResponse = await response.Content.ReadFromJsonAsync<BingSearchResponse>();
                var results = (bingResponse?.Value ?? [])
                    .Where(item => !string.IsNullOrEmpty(item.ContentUrl))
                    .Select(item => new ImageSearchResultDto
                    {
                        Title = item.Name,
                        ImageUrl = item.ContentUrl!,
                        ThumbnailUrl = item.ThumbnailUrl,
                        SourceUrl = item.HostPageUrl,
                    });

                return Ok(results);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(502, new { error = $"Failed to reach Bing API: {ex.Message}" });
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

        private record BingSearchResponse
        {
            [JsonPropertyName("value")]
            public List<BingImageResult>? Value { get; init; }
        }

        private record BingImageResult
        {
            [JsonPropertyName("name")]
            public string? Name { get; init; }

            [JsonPropertyName("contentUrl")]
            public string? ContentUrl { get; init; }

            [JsonPropertyName("thumbnailUrl")]
            public string? ThumbnailUrl { get; init; }

            [JsonPropertyName("hostPageUrl")]
            public string? HostPageUrl { get; init; }
        }
    }
}
