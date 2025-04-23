using System.ComponentModel.DataAnnotations; // For attributes like [Key], [Required]
using System.ComponentModel.DataAnnotations.Schema; // For [ForeignKey]

namespace Nimblist.Data.Models
{
    public class SubCategory // Changed from internal to public
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public Guid ParentCategoryId { get; set; } = Guid.Empty;

        [ForeignKey(nameof(ParentCategoryId))]
        public virtual Category? ParentCategory { get; set; } // Reference to the related ShoppingList

        public virtual ICollection<Item> Items { get; set; } = new List<Item>();

    }
}
