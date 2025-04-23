using System.ComponentModel.DataAnnotations; // For attributes like [Key], [Required]
using System.ComponentModel.DataAnnotations.Schema; // For [ForeignKey]

namespace Nimblist.Data.Models
{
    public class Category // Changed from internal to public
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        // Navigation property for the many-to-many relationship with ApplicationUser
        public virtual ICollection<SubCategory> SubCategories { get; set; } = new List<SubCategory>();

        public virtual ICollection<Item> Items { get; set; } = new List<Item>();

    }
}
