using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nimblist.Data.Models
{
    public class FamilyMember
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        public Guid FamilyId { get; set; }
        
        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }
        
        [ForeignKey(nameof(FamilyId))]
        public virtual Family? Family { get; set; }
        
        // Role of the member in the family (e.g., Admin, Member)
        [MaxLength(50)]
        public string Role { get; set; } = "Member";
        
        // When the user joined the family
        public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
