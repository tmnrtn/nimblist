using System.ComponentModel.DataAnnotations;

namespace Nimblist.api.DTO
{
    public class ClaimInviteRequest
    {
        [Required]
        public string Code { get; set; } = string.Empty;
    }
}
