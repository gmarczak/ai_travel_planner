using System.ComponentModel.DataAnnotations;

namespace project.Models
{
    public class AiResponseCache
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(64)]
        public string PromptHash { get; set; } = string.Empty;

        [Required]
        public string Response { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiresAt { get; set; }

        // Opcjonalnie: metadata
        public string? ModelName { get; set; }
        public int TokenCount { get; set; }
        public int HitCount { get; set; } = 0;
    }
}
