using Microsoft.AspNetCore.Identity;
using System.Collections.Generic; // Required for ICollection

namespace Nimblist.Data.Models
{
    // Inherit from IdentityUser to leverage ASP.NET Core Identity features
    public class ApplicationUser : IdentityUser
    {
        // You can add custom profile properties here if needed
        // e.g., public string? FirstName { get; set; }

        // Navigation property: A user can have multiple shopping lists
        // 'virtual' enables lazy loading (though explicit loading with .Include() is often preferred)
        // Initialize collection properties to avoid null reference exceptions
        public virtual ICollection<ShoppingList> ShoppingLists { get; set; } = new List<ShoppingList>();
    }
}