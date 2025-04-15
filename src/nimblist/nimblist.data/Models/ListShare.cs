using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nimblist.Data.Models
{
    public class ListShare
    {
        [Key]
        public Guid Id { get; set; }


        public string UserId { get; set; } = string.Empty;


        public Guid FamilyId { get; set; }
        public Guid ListId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }

        [ForeignKey(nameof(FamilyId))]
        public virtual Family? Family { get; set; }
        [ForeignKey(nameof(ListId))]
        public virtual ShoppingList? List { get; set; }

        // When the user joined the family
        public DateTimeOffset SharedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}