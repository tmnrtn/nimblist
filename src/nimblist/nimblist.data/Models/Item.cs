using Nimblist.Data.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nimblist.Data.Models
{
    public class Item
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)] // Optional field for quantity/notes like "1kg", "2 packs"
        public string? Quantity { get; set; }

        public bool IsChecked { get; set; } = false; // Default to not checked

        public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

        // --- Relationship with ShoppingList ---
        public Guid ShoppingListId { get; set; } // Foreign key property

        // Navigation property back to the list this item belongs to
        [ForeignKey(nameof(ShoppingListId))]
        public virtual ShoppingList? List { get; set; } // Reference to the related ShoppingList
    }
}