using Microsoft.EntityFrameworkCore;
using Nimblist.api.DTO;
using Nimblist.Data;

namespace Nimblist.api.Services
{
    public class ClassificationService : IClassificationService
    {
        private readonly NimblistContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ClassificationService> _logger;

        public ClassificationService(
            NimblistContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ClassificationService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<(Guid? CategoryId, Guid? SubCategoryId)> ClassifyAsync(string itemName)
        {
            var classificationServiceUrl = _configuration["ClassificationService:PredictUrl"];
            if (string.IsNullOrEmpty(classificationServiceUrl) || string.IsNullOrWhiteSpace(itemName))
                return (null, null);

            try
            {
                _logger.LogInformation("Classifying item: {ItemName}", itemName);
                var httpClient = _httpClientFactory.CreateClient("ClassificationServiceClient");
                var response = await httpClient.PostAsJsonAsync(classificationServiceUrl, new { product_name = itemName });

                if (response.IsSuccessStatusCode)
                    return await ParseClassificationResponseAsync(response);

                _logger.LogError("Classification service returned {StatusCode} for item '{ItemName}'.",
                    response.StatusCode, itemName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception classifying item: {ItemName}", itemName);
            }

            return (null, null);
        }

        private async Task<(Guid? CategoryId, Guid? SubCategoryId)> ParseClassificationResponseAsync(HttpResponseMessage response)
        {
            var result = await response.Content.ReadFromJsonAsync<ClassificationResponseDto>();
            if (result == null ||
                string.IsNullOrEmpty(result.PredictedPrimaryCategory) ||
                result.PredictedPrimaryCategory == "Unknown")
                return (null, null);

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Name.ToLower() == result.PredictedPrimaryCategory.ToLower());

            if (category == null)
            {
                _logger.LogWarning("Category '{CategoryName}' not found in database.", result.PredictedPrimaryCategory);
                return (null, null);
            }

            var subCategoryId = await ResolveSubCategoryAsync(result.PredictedSubCategory, category.Id);
            return (category.Id, subCategoryId);
        }

        private async Task<Guid?> ResolveSubCategoryAsync(string? predictedSubCategory, Guid categoryId)
        {
            if (string.IsNullOrEmpty(predictedSubCategory))
                return null;

            var subCategory = await _context.SubCategories
                .FirstOrDefaultAsync(sc =>
                    sc.Name.ToLower() == predictedSubCategory.ToLower() &&
                    sc.ParentCategoryId == categoryId);

            return subCategory?.Id;
        }
    }
}
