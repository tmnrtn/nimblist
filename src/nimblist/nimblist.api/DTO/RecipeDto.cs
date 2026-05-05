using System.Text.Json.Serialization;

namespace Nimblist.api.DTO
{
    public record RecipeIngredientDto(Guid Id, string Text, string? ParsedName, string? ParsedQuantity, int SortOrder);

    public record RecipeIngredientInputDto(string Text, string? ParsedName, string? ParsedQuantity, int SortOrder);

    public record CreateRecipeRequest(
        string Title,
        string? Description,
        string? SourceUrl,
        string? ImageUrl,
        string? Yields,
        int? TotalTimeMinutes,
        string? Instructions,
        List<RecipeIngredientInputDto> Ingredients
    );

    public record RecipeSummaryDto(
        Guid Id,
        string Title,
        string? ImageUrl,
        string? Yields,
        int? TotalTimeMinutes,
        int IngredientCount,
        DateTimeOffset CreatedAt,
        bool IsOwned
    );

    public record RecipeDetailDto(
        Guid Id,
        string Title,
        string? Description,
        string? SourceUrl,
        string? ImageUrl,
        string? Yields,
        int? TotalTimeMinutes,
        string? Instructions,
        DateTimeOffset CreatedAt,
        List<RecipeIngredientDto> Ingredients,
        bool IsOwned
    );

    public class ScraperIngredientDto
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("parsed_name")]
        public string? ParsedName { get; set; }

        [JsonPropertyName("parsed_quantity")]
        public string? ParsedQuantity { get; set; }
    }

    public class ScraperResponseDto
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("image")]
        public string? Image { get; set; }

        [JsonPropertyName("yields")]
        public string? Yields { get; set; }

        [JsonPropertyName("total_time")]
        public int? TotalTime { get; set; }

        [JsonPropertyName("ingredients")]
        public List<ScraperIngredientDto> Ingredients { get; set; } = new();

        [JsonPropertyName("instructions")]
        public string? Instructions { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
