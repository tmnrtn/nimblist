using System.ComponentModel.DataAnnotations;

namespace Nimblist.Data.Models
{
    public class LlmSettings
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(50)]
        public string? Provider { get; set; }

        [MaxLength(200)]
        public string? Model { get; set; }

        [MaxLength(200)]
        public string? VisionModel { get; set; }

        [MaxLength(500)]
        public string? ApiKey { get; set; }

        [MaxLength(300)]
        public string? BaseUrl { get; set; }

        [MaxLength(500)]
        public string? ImageSearchApiKey { get; set; }

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
