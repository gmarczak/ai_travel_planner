namespace project.Models
{
    /// <summary>
    /// Cache table for destination images from Unsplash
    /// </summary>
    public class DestinationImage
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Normalized destination name (lowercase, trimmed)
        /// </summary>
        public string Destination { get; set; } = string.Empty;
        
        /// <summary>
        /// Image URL from Unsplash
        /// </summary>
        public string ImageUrl { get; set; } = string.Empty;
        
        /// <summary>
        /// Photographer name for attribution (Unsplash requirement)
        /// </summary>
        public string? PhotographerName { get; set; }
        
        /// <summary>
        /// Photographer profile URL for attribution
        /// </summary>
        public string? PhotographerUrl { get; set; }
        
        /// <summary>
        /// Source of the image (Unsplash, GoogleMaps, etc.)
        /// </summary>
        public string Source { get; set; } = "Unsplash";
        
        /// <summary>
        /// When this image was cached
        /// </summary>
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Number of times this cached image was used
        /// </summary>
        public int UsageCount { get; set; } = 0;
    }
}
