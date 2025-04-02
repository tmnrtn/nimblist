using System.ComponentModel.DataAnnotations;

namespace Nimblist.api.DTO
{
    public class ShoppingListInputDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        // Only include properties the client should provide
    }
}
