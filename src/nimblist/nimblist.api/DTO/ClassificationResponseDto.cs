using System.Text.Json.Serialization;

namespace Nimblist.api.DTO
{
    public class ClassificationResponseDto
    {
        [JsonPropertyName("input_product_name")]
        public string? InputProductName { get; set; }

        [JsonPropertyName("cleaned_product_name")]
        public string? CleanedProductName { get; set; }

        [JsonPropertyName("predicted_primary_category")]
        public string? PredictedPrimaryCategory { get; set; }

        [JsonPropertyName("predicted_sub_category")]
        public string? PredictedSubCategory { get; set; }
    }
}