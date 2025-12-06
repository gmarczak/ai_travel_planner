using Microsoft.EntityFrameworkCore;
using project.Data;
using project.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace project.Services
{
    /// <summary>
    /// Google Directions API service to fetch road-based routes with caching
    /// </summary>
    public class GoogleDirectionsService : IDirectionsService
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleDirectionsService> _logger;
        private readonly string? _googleMapsApiKey;
        private static bool _apiAuthorizationChecked = false;
        private static bool _apiAuthorized = false;

        public GoogleDirectionsService(
            HttpClient httpClient,
            ApplicationDbContext dbContext,
            IConfiguration configuration,
            ILogger<GoogleDirectionsService> logger)
        {
            _httpClient = httpClient;
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;

            // Prefer a dedicated server-side key for Directions API
            _googleMapsApiKey =
                configuration["GoogleMaps_ServerApiKey"] ??
                configuration["GoogleMaps:ServerApiKey"] ??
                configuration["GoogleMaps_ApiKey"] ??
                configuration["GoogleMaps:ApiKey"];

            if (string.IsNullOrEmpty(_googleMapsApiKey))
            {
                _logger.LogWarning("Google Maps API key not configured - directions will not be available");
            }
        }

        public async Task<string?> GetRoutePolylineAsync(string startLocation, string endLocation)
        {
            if (string.IsNullOrWhiteSpace(startLocation) || string.IsNullOrWhiteSpace(endLocation))
            {
                _logger.LogWarning("GetRoutePolylineAsync called with empty location");
                return null;
            }

            if (string.IsNullOrEmpty(_googleMapsApiKey))
            {
                _logger.LogDebug("Google Maps API key not configured, returning null polyline");
                return null;
            }

            // If API is not authorized, skip requests to avoid quota waste
            if (_apiAuthorizationChecked && !_apiAuthorized)
            {
                return null;
            }

            var cacheKey = $"{startLocation.Trim().ToLowerInvariant()}|{endLocation.Trim().ToLowerInvariant()}";

            // Check cache first (valid for 30 days)
            var cached = await _dbContext.RoutePolylines
                .Where(r => r.RouteKey == cacheKey)
                .Where(r => r.CachedAt > DateTime.UtcNow.AddDays(-30))
                .FirstOrDefaultAsync();

            if (cached != null)
            {
                _logger.LogInformation("Using cached route polyline for {Start} -> {End}", startLocation, endLocation);
                cached.UsageCount++;
                await _dbContext.SaveChangesAsync();
                return cached.EncodedPolyline;
            }

            // Fetch from Google Directions API
            _logger.LogInformation("Fetching route from Google Directions API: {Start} -> {End}", startLocation, endLocation);

            try
            {
                var origin = Uri.EscapeDataString(startLocation);
                var destination = Uri.EscapeDataString(endLocation);
                var url = $"https://maps.googleapis.com/maps/api/directions/json?origin={origin}&destination={destination}&key={_googleMapsApiKey}";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Google Directions API returned {StatusCode}: {Error}", response.StatusCode, errorBody);
                    return null;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<DirectionsResponse>(responseBody,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Check for API authorization issues
                if (result?.Status == "REQUEST_DENIED")
                {
                    if (!_apiAuthorizationChecked)
                    {
                        _apiAuthorizationChecked = true;
                        _apiAuthorized = false;
                        var err = result.ErrorMessage ?? "REQUEST_DENIED";
                        if (err.Contains("referer", StringComparison.OrdinalIgnoreCase) || err.Contains("referrer", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogError("Google Directions API request denied due to HTTP referrer restrictions. Configure a server-side key (no referrer restriction) via 'GoogleMaps:ServerApiKey' and restrict it by server IP if needed. Error: {Error}", err);
                        }
                        else
                        {
                            _logger.LogError("Google Directions API is not enabled or not authorized for this key. Enable 'Directions API' and/or provide a server-side key via 'GoogleMaps:ServerApiKey'. Error: {Error}", err);
                        }
                    }
                    return null;
                }

                if (!_apiAuthorizationChecked)
                {
                    _apiAuthorizationChecked = true;
                    _apiAuthorized = true;
                    _logger.LogInformation("Google Directions API is properly configured and authorized");
                }

                if (result?.Routes == null || result.Routes.Count == 0)
                {
                    _logger.LogDebug("No route found for {Start} -> {End}. API status: {Status}",
                        startLocation, endLocation, result?.Status ?? "null");
                    return null;
                }

                var polyline = result.Routes[0]?.OverviewPolyline?.Points;

                if (string.IsNullOrEmpty(polyline))
                {
                    _logger.LogWarning("Route found but no polyline data for {Start} -> {End}", startLocation, endLocation);
                    return null;
                }

                // Cache the polyline
                await CachePolylineAsync(cacheKey, polyline);

                _logger.LogInformation("Fetched and cached route polyline for {Start} -> {End}", startLocation, endLocation);
                return polyline;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching route from Google Directions API for {Start} -> {End}", startLocation, endLocation);
                return null;
            }
        }

        private async Task CachePolylineAsync(string routeKey, string encodedPolyline)
        {
            try
            {
                var existing = await _dbContext.RoutePolylines.FirstOrDefaultAsync(r => r.RouteKey == routeKey);

                if (existing != null)
                {
                    existing.EncodedPolyline = encodedPolyline;
                    existing.CachedAt = DateTime.UtcNow;
                    existing.UsageCount++;
                }
                else
                {
                    _dbContext.RoutePolylines.Add(new RoutePolyline
                    {
                        RouteKey = routeKey,
                        EncodedPolyline = encodedPolyline,
                        CachedAt = DateTime.UtcNow,
                        UsageCount = 1
                    });
                }
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching route polyline for {RouteKey}", routeKey);
            }
        }

        // Google Directions API response models
        private class DirectionsResponse
        {
            [JsonPropertyName("routes")]
            public List<DirectionsRoute>? Routes { get; set; }

            [JsonPropertyName("status")]
            public string? Status { get; set; }

            [JsonPropertyName("error_message")]
            public string? ErrorMessage { get; set; }
        }

        private class DirectionsRoute
        {
            [JsonPropertyName("overview_polyline")]
            public DirectionsPolyline? OverviewPolyline { get; set; }
        }

        private class DirectionsPolyline
        {
            [JsonPropertyName("points")]
            public string? Points { get; set; }
        }
    }
}
