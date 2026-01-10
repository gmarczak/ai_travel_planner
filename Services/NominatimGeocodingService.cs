using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace project.Services
{
    public sealed class NominatimGeocodingService : IGeocodingService
    {
        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly ILogger<NominatimGeocodingService> _logger;

        // Nominatim usage policy: be nice (throttle). This is a coarse global throttle.
        private static readonly SemaphoreSlim ThrottleGate = new(1, 1);
        private static DateTimeOffset _lastRequestAt = DateTimeOffset.MinValue;

        public NominatimGeocodingService(HttpClient http, IMemoryCache cache, ILogger<NominatimGeocodingService> logger)
        {
            _http = http;
            _cache = cache;
            _logger = logger;
        }

        public async Task<(double Latitude, double Longitude)?> GeocodeAsync(string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;

            var cacheKey = $"geocode:nominatim:{query.Trim().ToLowerInvariant()}";
            if (_cache.TryGetValue<(double Latitude, double Longitude)?>(cacheKey, out var cached))
            {
                return cached;
            }

            await ThrottleGate.WaitAsync(cancellationToken);
            try
            {
                var elapsedMs = (DateTimeOffset.UtcNow - _lastRequestAt).TotalMilliseconds;
                if (elapsedMs < 1100)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1100 - elapsedMs), cancellationToken);
                }

                var url = $"https://nominatim.openstreetmap.org/search?format=jsonv2&q={Uri.EscapeDataString(query)}&limit=1";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd("TravelPlannerApp/1.0");
                req.Headers.Accept.ParseAdd("application/json");

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                _lastRequestAt = DateTimeOffset.UtcNow;

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Nominatim geocode failed ({Status}) for query: {Query}", (int)resp.StatusCode, query);
                    _cache.Set<(double Latitude, double Longitude)?>(cacheKey, null, TimeSpan.FromHours(6));
                    return null;
                }

                await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                {
                    _cache.Set<(double Latitude, double Longitude)?>(cacheKey, null, TimeSpan.FromDays(1));
                    return null;
                }

                var first = doc.RootElement[0];
                if (!first.TryGetProperty("lat", out var latEl) || !first.TryGetProperty("lon", out var lonEl))
                {
                    _cache.Set<(double Latitude, double Longitude)?>(cacheKey, null, TimeSpan.FromDays(1));
                    return null;
                }

                if (!double.TryParse(latEl.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat) ||
                    !double.TryParse(lonEl.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lon))
                {
                    _cache.Set<(double Latitude, double Longitude)?>(cacheKey, null, TimeSpan.FromDays(1));
                    return null;
                }

                var result = (Latitude: lat, Longitude: lon);
                _cache.Set<(double Latitude, double Longitude)?>(cacheKey, result, TimeSpan.FromDays(14));
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Nominatim geocode exception for query: {Query}", query);
                _cache.Set<(double Latitude, double Longitude)?>(cacheKey, null, TimeSpan.FromHours(1));
                return null;
            }
            finally
            {
                ThrottleGate.Release();
            }
        }
    }
}
