using System.ComponentModel.DataAnnotations;

namespace Nimblist.Data.Models
{
    public class MealPlan
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public virtual ApplicationUser? User { get; set; }

        public virtual ICollection<MealPlanEntry> Entries { get; set; } = new List<MealPlanEntry>();

        public virtual ICollection<MealPlanShare> Shares { get; set; } = new List<MealPlanShare>();
    }
}
