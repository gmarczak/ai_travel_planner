namespace project.Services
{
    public interface IImageService
    {
        /// <summary>
        /// Gets destination image from cache or Unsplash API with Google Maps fallback
        /// </summary>
        /// <param name="destination">Destination name (e.g., "Paris, France")</param>
        /// <returns>Image URL or null if all sources fail</returns>
        Task<string?> GetDestinationImageAsync(string destination);

        /// <summary>
        /// Gets cached image with photographer attribution
        /// </summary>
        Task<(string? ImageUrl, string? PhotographerName, string? PhotographerUrl)?> GetDestinationImageWithAttributionAsync(string destination);

        /// <summary>
        /// Gets multiple images in parallel for better performance (2-3 images per day strategy)
        /// </summary>
        /// <param name="queries">List of search queries (e.g., "Eiffel Tower Paris", "French cuisine restaurant")</param>
        /// <returns>Dictionary mapping query to image URL</returns>
        Task<Dictionary<string, string>> GetMultipleImagesAsync(IEnumerable<string> queries);
    }
}
