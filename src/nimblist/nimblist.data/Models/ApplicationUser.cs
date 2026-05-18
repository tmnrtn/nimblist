using Microsoft.AspNetCore.Identity;
using System.Collections.Generic; // Required for ICollection

namespace Nimblist.Data.Models
{
    // Inherit from IdentityUser to leverage ASP.NET Core Identity features
    public class ApplicationUser : IdentityUser
    {
        public bool IsComplimentaryAccess { get; set; }

        public string? InviteCode { get; set; }
        public string? InvitedByUserId { get; set; }

        // Navigation property: A user can have multiple shopping lists
        public virtual ICollection<ShoppingList> ShoppingLists { get; set; } = new List<ShoppingList>();

        // Navigation property for the many-to-many relationship with Family
        public virtual ICollection<FamilyMember> Families { get; set; } = new List<FamilyMember>();

        public virtual ICollection<ListShare> ListShares { get; set; } = new List<ListShare>();

        public virtual ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();

        public virtual ICollection<MealPlan> MealPlans { get; set; } = new List<MealPlan>();
        public virtual ICollection<UserPushSubscription> PushSubscriptions { get; set; } = new List<UserPushSubscription>();
    }
}