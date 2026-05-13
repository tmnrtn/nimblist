namespace Nimblist.Data.Models
{
    public class UserPushSubscription
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
        public string Endpoint { get; set; } = null!;
        public string P256dh { get; set; } = null!;
        public string Auth { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
