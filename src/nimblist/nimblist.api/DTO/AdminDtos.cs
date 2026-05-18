namespace Nimblist.api.DTO
{
    public class AdminUserDto
    {
        public string UserId { get; set; } = string.Empty;
        public string? Email { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
        public bool IsComplimentaryAccess { get; set; }
        public bool IsDisabled { get; set; }
    }

    public class SetUserStatusDto
    {
        public bool Disabled { get; set; }
    }

    public class SetComplimentaryAccessDto
    {
        public bool IsComplimentaryAccess { get; set; }
    }

    public class AdminFamilyMemberDto
    {
        public Guid MemberId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Role { get; set; } = string.Empty;
        public DateTimeOffset JoinedAt { get; set; }
    }

    public class AdminFamilyDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string OwnerUserId { get; set; } = string.Empty;
        public string? OwnerEmail { get; set; }
        public List<AdminFamilyMemberDto> Members { get; set; } = new();
    }

    public class SetRoleDto
    {
        public string Role { get; set; } = string.Empty;
    }

    public class AdminFeedbackDto
    {
        public Guid Id { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public string? SubCategoryName { get; set; }
        public string? UserEmail { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class LlmSettingsDto
    {
        public string? Provider { get; set; }
        public string? Model { get; set; }
        public string? VisionModel { get; set; }
        public string? ApiKey { get; set; }
        public string? BaseUrl { get; set; }
        public string? ImageSearchApiKey { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
