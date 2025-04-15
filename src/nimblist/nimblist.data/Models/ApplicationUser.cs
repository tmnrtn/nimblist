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
        public virtual ICollection<ShoppingList> ShoppingLists { get; set; } = new List<ShoppingList>();

        // Navigation property for the many-to-many relationship with Family
        public virtual ICollection<FamilyMember> Families { get; set; } = new List<FamilyMember>();

        public virtual ICollection<ListShare> ListShares { get; set; } = new List<ListShare>();
    }
}