using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nimblist.Data.Models
{
    public class ItemClassificationFeedback
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string ItemName { get; set; } = string.Empty;

        public Guid? CategoryId { get; set; }

        public Guid? SubCategoryId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [ForeignKey(nameof(CategoryId))]
        public virtual Category? Category { get; set; }

        [ForeignKey(nameof(SubCategoryId))]
        public virtual SubCategory? SubCategory { get; set; }
    }
}
