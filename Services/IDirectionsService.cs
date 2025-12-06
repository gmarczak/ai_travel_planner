namespace project.Services
{
    /// <summary>
    /// Interface for fetching road-based routes between locations
    /// </summary>
    public interface IDirectionsService
    {
        /// <summary>
        /// Get the encoded polyline for the route between start and end locations.
        /// Returns null if no route found or API unavailable.
        /// </summary>
        Task<string?> GetRoutePolylineAsync(string startLocation, string endLocation);
    }
}
