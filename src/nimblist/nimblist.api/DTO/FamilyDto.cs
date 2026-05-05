namespace Nimblist.api.DTO
{
    public record FamilyMemberDetailDto(
        Guid Id,
        string UserId,
        string? Email,
        string Role,
        DateTimeOffset JoinedAt,
        bool IsOwner
    );

    public record FamilyWithMembersDto(
        Guid Id,
        string Name,
        string OwnerId,
        List<FamilyMemberDetailDto> Members
    );
}
