namespace Nimblist.api.DTO
{
    public class UserInfoDto
    {
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
        public string SubscriptionTier { get; set; } = "free"; // "free" | "paid"
        public bool IsInTrial { get; set; }
        public DateTime? TrialEndDate { get; set; }
    }
}
