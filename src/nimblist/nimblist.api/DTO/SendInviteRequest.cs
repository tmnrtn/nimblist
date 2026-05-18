using System.ComponentModel.DataAnnotations;

namespace Nimblist.api.DTO
{
    public class SendInviteRequest
    {
        [Required]
        public string Email { get; set; } = string.Empty;
    }
}
