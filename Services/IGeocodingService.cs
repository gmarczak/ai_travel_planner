namespace project.Services
{
    public interface IGeocodingService
    {
        /// <summary>
        /// Geocodes a free-form query (e.g. "Eiffel Tower, Paris"). Returns null if not found.
        /// </summary>
        Task<(double Latitude, double Longitude)?> GeocodeAsync(string query, CancellationToken cancellationToken = default);
    }
}
