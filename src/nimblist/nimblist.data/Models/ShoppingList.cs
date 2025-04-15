using Nimblist.Data.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // For attributes like [Key], [Required]
using System.ComponentModel.DataAnnotations.Schema; // For [ForeignKey]

namespace Nimblist.Data.Models
{
    public class ShoppingList
    {
        [Key] // Defines the primary key
        public Guid Id { get; set; } // Using Guid as the primary key type

        [Required] // Makes the Name property mandatory
        [MaxLength(100)] // Sets a maximum length for the Name
        public string Name { get; set; } = string.Empty; // Initialize to avoid null warnings

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; // Default to current time

        // --- Relationship with ApplicationUser ---
        [Required]
        public string UserId { get; set; } = string.Empty; // Foreign key property (matches IdentityUser's Id type - string)

        // Navigation property back to the user who owns this list
        [ForeignKey(nameof(UserId))] // Links this navigation property to the UserId foreign key
        public virtual ApplicationUser? User { get; set; } // Reference to the related ApplicationUser

        // --- Relationship with Item ---
        // Navigation property: A shopping list contains multiple items
        public virtual ICollection<Item> Items { get; set; } = new List<Item>(); // Initialize collection

        public virtual ICollection<ListShare> ListShares { get; set; } = new List<ListShare>();
    }
}