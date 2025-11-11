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
    }
}
