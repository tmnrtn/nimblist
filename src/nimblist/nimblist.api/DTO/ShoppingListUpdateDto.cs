using System.ComponentModel.DataAnnotations;

namespace Nimblist.api.DTO
{
    public class ShoppingListUpdateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        public bool IsTemplate { get; set; } = false;
    }
}
