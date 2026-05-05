namespace Nimblist.api.DTO
{
    public record MealPlanSummaryDto(Guid Id, string Name, string OwnerId, bool IsOwned, DateTimeOffset CreatedAt);

    public record MealPlanEntryDto(
        Guid Id,
        Guid MealPlanId,
        Guid RecipeId,
        string RecipeTitle,
        string? RecipeImageUrl,
        DateOnly PlannedDate,
        string? MealType,
        string? Notes
    );

    public record CreateMealPlanRequest(string Name);

    public record CreateMealPlanEntryRequest(
        Guid MealPlanId,
        Guid RecipeId,
        DateOnly PlannedDate,
        string? MealType,
        string? Notes
    );

    public record MealPlanShareDetailDto(
        Guid Id,
        Guid MealPlanId,
        string? SharedWithUserId,
        string? SharedWithEmail,
        Guid? SharedWithFamilyId,
        string? SharedWithFamilyName,
        DateTimeOffset SharedAt
    );

    public class MealPlanShareInputDto
    {
        public Guid MealPlanId { get; set; }
        public string? UserIdToShareWith { get; set; }
        public Guid? FamilyIdToShareWith { get; set; }
    }
}
