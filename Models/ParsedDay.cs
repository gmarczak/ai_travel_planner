namespace project.Models
{
    public record ParsedDay(int Day, string Date, string[] Lines)
    {
        /// <summary>
        /// Images for this day (2-3 images: main attraction, food/activity, optional third)
        /// </summary>
        public List<DayImage> Images { get; init; } = new();
    }

    public class DayImage
    {
        /// <summary>
        /// Image URL from Unsplash or fallback
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Description/caption for the image (e.g., "Eiffel Tower", "French cuisine")
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// True if this is the primary/hero image for the day
        /// </summary>
        public bool IsPrimary { get; set; }

        /// <summary>
        /// Search query used to fetch this image
        /// </summary>
        public string Query { get; set; } = string.Empty;
    }
}
