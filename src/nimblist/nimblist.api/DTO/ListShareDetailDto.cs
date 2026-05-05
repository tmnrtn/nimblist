namespace Nimblist.api.DTO
{
    public record ListShareDetailDto(
        Guid Id,
        Guid ListId,
        string? SharedWithUserId,
        string? SharedWithEmail,
        Guid? SharedWithFamilyId,
        string? SharedWithFamilyName,
        DateTimeOffset SharedAt
    );
}
