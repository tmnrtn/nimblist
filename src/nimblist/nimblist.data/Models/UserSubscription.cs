namespace Nimblist.Data.Models
{
    public class UserSubscription
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
        public string PayPalSubscriptionId { get; set; } = null!;
        // Mirrors PayPal status: APPROVAL_PENDING, APPROVED, ACTIVE, SUSPENDED, CANCELLED, EXPIRED
        public string Status { get; set; } = "APPROVAL_PENDING";
        public bool IsInTrial { get; set; }
        public DateTime? TrialEndDate { get; set; }
        public DateTime? NextBillingDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
