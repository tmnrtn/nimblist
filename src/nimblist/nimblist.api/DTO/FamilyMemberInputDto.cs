using System;
using System.ComponentModel.DataAnnotations;

namespace Nimblist.api.DTO
{
    public class FamilyMemberInputDto
    {
        [Required]
        public Guid FamilyId { get; set; }

        [Required]
        public string UserIdToAdd { get; set; } = string.Empty; // UserId (string) of the ApplicationUser to add
    }
}