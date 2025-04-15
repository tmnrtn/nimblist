using System.ComponentModel.DataAnnotations; // For attributes like [Key], [Required]
using System.ComponentModel.DataAnnotations.Schema; // For [ForeignKey]

namespace Nimblist.Data.Models
{
    public class Family // Changed from internal to public
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        // Navigation property for the many-to-many relationship with ApplicationUser
        public virtual ICollection<FamilyMember> Members { get; set; } = new List<FamilyMember>();

        public virtual ICollection<ListShare> ListShares { get; set; } = new List<ListShare>();
    }
}
