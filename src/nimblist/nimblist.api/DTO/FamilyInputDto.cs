using System.ComponentModel.DataAnnotations;

namespace Nimblist.api.DTO
{
    public class FamilyInputDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}