using System.ComponentModel.DataAnnotations;

namespace project.Models
{
    /// <summary>
    /// Cached route polyline from Google Directions API
    /// </summary>
    public class RoutePolyline
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Composite key: "startLocation|endLocation" (normalized lowercase)
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string RouteKey { get; set; } = string.Empty;

        /// <summary>
        /// Google Maps encoded polyline string
        /// </summary>
        [Required]
        public string EncodedPolyline { get; set; } = string.Empty;

        /// <summary>
        /// When this route was first cached
        /// </summary>
        public DateTime CachedAt { get; set; }

        /// <summary>
        /// How many times this route has been requested
        /// </summary>
        public int UsageCount { get; set; }
    }
}
