using System.ComponentModel.DataAnnotations;

namespace Nimblist.Data.Models
{
    public class Tag
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Colour name from the fixed palette (e.g. "red", "blue"). Null = default grey.</summary>
        [MaxLength(20)]
        public string? Color { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public virtual ApplicationUser? User { get; set; }

        public virtual ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
    }
}
