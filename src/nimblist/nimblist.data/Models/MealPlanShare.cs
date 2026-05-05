using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nimblist.Data.Models
{
    public class MealPlanShare
    {
        [Key]
        public Guid Id { get; set; }

        public Guid MealPlanId { get; set; }

        public string? UserId { get; set; }

        public Guid? FamilyId { get; set; }

        public DateTimeOffset SharedAt { get; set; } = DateTimeOffset.UtcNow;

        [ForeignKey(nameof(MealPlanId))]
        public virtual MealPlan? MealPlan { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }

        [ForeignKey(nameof(FamilyId))]
        public virtual Family? Family { get; set; }
    }
}
