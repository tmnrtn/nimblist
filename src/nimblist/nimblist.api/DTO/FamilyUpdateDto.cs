using System.ComponentModel.DataAnnotations;

namespace Nimblist.api.DTO
{
    public class FamilyUpdateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}