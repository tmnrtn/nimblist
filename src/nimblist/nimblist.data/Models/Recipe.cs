using System.ComponentModel.DataAnnotations;

namespace Nimblist.Data.Models
{
    public class Recipe
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [MaxLength(2048)]
        public string? SourceUrl { get; set; }

        [MaxLength(2048)]
        public string? ImageUrl { get; set; }

        [MaxLength(100)]
        public string? Yields { get; set; }

        public int? TotalTimeMinutes { get; set; }

        public string? Instructions { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public virtual ApplicationUser? User { get; set; }

        public virtual ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();

        public virtual ICollection<RecipeShare> Shares { get; set; } = new List<RecipeShare>();
    }
}
