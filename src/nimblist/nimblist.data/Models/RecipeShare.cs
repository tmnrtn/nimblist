using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nimblist.Data.Models
{
    public class RecipeShare
    {
        [Key]
        public Guid Id { get; set; }

        public Guid RecipeId { get; set; }

        public string? UserId { get; set; }

        public Guid? FamilyId { get; set; }

        public DateTimeOffset SharedAt { get; set; } = DateTimeOffset.UtcNow;

        [ForeignKey(nameof(RecipeId))]
        public virtual Recipe? Recipe { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }

        [ForeignKey(nameof(FamilyId))]
        public virtual Family? Family { get; set; }
    }
}
