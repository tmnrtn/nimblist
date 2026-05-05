using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nimblist.Data.Models
{
    public class RecipeIngredient
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Text { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? ParsedName { get; set; }

        [MaxLength(100)]
        public string? ParsedQuantity { get; set; }

        public int SortOrder { get; set; }

        public Guid RecipeId { get; set; }

        [ForeignKey(nameof(RecipeId))]
        public virtual Recipe? Recipe { get; set; }
    }
}
