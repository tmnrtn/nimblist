using System.ComponentModel.DataAnnotations;

namespace Nimblist.api.DTO
{
    public record RecipeShareDetailDto(
        Guid Id,
        Guid RecipeId,
        string? SharedWithUserId,
        string? SharedWithEmail,
        Guid? SharedWithFamilyId,
        string? SharedWithFamilyName,
        DateTimeOffset SharedAt
    );

    public class RecipeShareInputDto
    {
        [Required]
        public Guid RecipeId { get; set; }

        public string? UserIdToShareWith { get; set; }

        public Guid? FamilyIdToShareWith { get; set; }
    }

    public class RecipeBulkShareInputDto
    {
        public string? UserIdToShareWith { get; set; }

        public Guid? FamilyIdToShareWith { get; set; }
    }

    public record RecipeBulkShareResultDto(int SharedCount, int SkippedCount);
}
