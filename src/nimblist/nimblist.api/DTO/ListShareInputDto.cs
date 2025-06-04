using System;
using System.ComponentModel.DataAnnotations;

namespace Nimblist.api.DTO
{
    public class ListShareInputDto
    {
        [Required]
        public Guid ListId { get; set; }

        public string? UserIdToShareWith { get; set; } // ApplicationUser.Id (string)

        public Guid? FamilyIdToShareWith { get; set; }

        // Custom validation can be added here or in the controller to ensure
        // one of UserIdToShareWith or FamilyIdToShareWith is set, but not both.
    }
}