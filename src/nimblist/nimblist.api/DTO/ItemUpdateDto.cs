using Nimblist.Data.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nimblist.api.DTO
{
    public class ItemUpdateDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)] // Optional field for quantity/notes like "1kg", "2 packs"
        public string? Quantity { get; set; }

        public bool IsChecked { get; set; } = false; // Default to not checked

        public Guid ShoppingListId { get; set; } // Foreign key property

    }
}
