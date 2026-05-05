using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nimblist.Data.Models
{
    public class MealPlanEntry
    {
        [Key]
        public Guid Id { get; set; }

        public Guid MealPlanId { get; set; }

        public Guid RecipeId { get; set; }

        public DateOnly PlannedDate { get; set; }

        [MaxLength(50)]
        public string? MealType { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [ForeignKey(nameof(MealPlanId))]
        public virtual MealPlan? MealPlan { get; set; }

        [ForeignKey(nameof(RecipeId))]
        public virtual Recipe? Recipe { get; set; }
    }
}
