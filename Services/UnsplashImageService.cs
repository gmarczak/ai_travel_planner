using Microsoft.EntityFrameworkCore;
using project.Data;
using project.Models;
using System.Text.Json.Serialization;

namespace project.Services
{
    public class UnsplashImageService : IImageService
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UnsplashImageService> _logger;
        private readonly string? _unsplashAccessKey;
        private readonly string? _googleMapsApiKey;
        private static readonly SemaphoreSlim _cacheSemaphore = new SemaphoreSlim(1, 1);

        public UnsplashImageService(
            HttpClient httpClient,
            ApplicationDbContext db,
            IConfiguration configuration,
            ILogger<UnsplashImageService> logger)
        {
            _httpClient = httpClient;
            _db = db;
            _configuration = configuration;
            _logger = logger;

            // Azure App Settings don't allow ':' in key names, so check both formats
            _unsplashAccessKey = configuration["Unsplash_AccessKey"] ?? configuration["Unsplash:AccessKey"];
            _googleMapsApiKey = configuration["GoogleMaps_ApiKey"] ?? configuration["GoogleMaps:ApiKey"];

            if (!string.IsNullOrEmpty(_unsplashAccessKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Client-ID {_unsplashAccessKey}");
                _logger.LogInformation("Unsplash API key configured successfully");
            }
            else
            {
                _logger.LogWarning("Unsplash API key not found (checked Unsplash_AccessKey and Unsplash:AccessKey)");
            }
        }

        public async Task<string?> GetDestinationImageAsync(string destination)
        {
            var result = await GetDestinationImageWithAttributionAsync(destination);
            return result?.ImageUrl;
        }

        public async Task<(string? ImageUrl, string? PhotographerName, string? PhotographerUrl)?> GetDestinationImageWithAttributionAsync(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination))
            {
                _logger.LogWarning("GetDestinationImageWithAttributionAsync called with empty destination");
                return null;
            }

            _logger.LogInformation("Fetching image for destination: {Destination}", destination);

            // Clean noisy queries (remove emojis, long prefixes like "ℹ️", "📍", non-word chars, excessive length)
            var cleanedOriginal = CleanQuery(destination);
            if (!string.Equals(cleanedOriginal, destination, StringComparison.Ordinal))
            {
                _logger.LogDebug("Cleaned incoming destination query from '{Original}' to '{Cleaned}'", destination, cleanedOriginal);
                destination = cleanedOriginal;
            }

            var normalizedDestination = NormalizeDestination(destination);
            _logger.LogDebug("Normalized destination: {Normalized}", normalizedDestination);

            // 1. Check cache first (expires after 90 days) - serialize to avoid concurrency
            await _cacheSemaphore.WaitAsync();
            try
            {
                var cachedImage = await _db.DestinationImages
                    .Where(di => di.Destination == normalizedDestination)
                    .Where(di => di.CachedAt > DateTime.UtcNow.AddDays(-90))
                    .FirstOrDefaultAsync();

                if (cachedImage != null)
                {
                    _logger.LogInformation("Using cached image for {Destination} from {Source}", destination, cachedImage.Source);

                    // Update usage count
                    cachedImage.UsageCount++;
                    await _db.SaveChangesAsync();

                    return (cachedImage.ImageUrl, cachedImage.PhotographerName, cachedImage.PhotographerUrl);
                }
            }
            finally
            {
                _cacheSemaphore.Release();
            }

            // 2. Try Unsplash API
            _logger.LogInformation("No cached image found, trying Unsplash API for {Destination}", destination);
            var unsplashResult = await TryGetUnsplashImageAsync(destination);
            if (unsplashResult != null)
            {
                await CacheImageAsync(normalizedDestination, unsplashResult.Value.ImageUrl, "Unsplash",
                    unsplashResult.Value.PhotographerName, unsplashResult.Value.PhotographerUrl);
                return unsplashResult;
            }

            // 3. Fallback to Google Maps Static API
            _logger.LogInformation("Unsplash returned null, trying Google Maps fallback for {Destination}", destination);
            var googleMapsUrl = TryGetGoogleMapsStaticImage(destination);
            if (googleMapsUrl != null)
            {
                _logger.LogInformation("Using Google Maps fallback for {Destination}", destination);
                await CacheImageAsync(normalizedDestination, googleMapsUrl, "GoogleMaps", null, null);
                return (googleMapsUrl, null, null);
            }

            _logger.LogWarning("No image found for {Destination}", destination);
            return null;
        }

        private async Task<(string ImageUrl, string? PhotographerName, string? PhotographerUrl)?> TryGetUnsplashImageAsync(string destination)
        {
            if (string.IsNullOrEmpty(_unsplashAccessKey))
            {
                _logger.LogWarning("Unsplash API key not configured - cannot fetch images");
                return null;
            }

            // Final sanitization pass before building Unsplash query
            var cleaned = CleanQuery(destination);
            _logger.LogInformation("Calling Unsplash API for {Destination} (sanitized: '{Cleaned}') with key length: {KeyLength}", destination, cleaned, _unsplashAccessKey.Length);

            try
            {
                var query = Uri.EscapeDataString(cleaned);
                var url = $"https://api.unsplash.com/search/photos?query={query}&per_page=5&orientation=landscape";
                _logger.LogDebug("Unsplash API URL: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                _logger.LogInformation("Unsplash API response status: {StatusCode} for {Destination}", response.StatusCode, destination);

                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Unsplash API returned {StatusCode} for {Destination}. Response: {Response}", response.StatusCode, destination, responseBody);
                    return null;
                }

                var result = await response.Content.ReadFromJsonAsync<UnsplashSearchResponse>();

                if (result?.Results == null || result.Results.Count == 0)
                {
                    _logger.LogInformation("No Unsplash images found for {Destination}", destination);
                    return null;
                }

                // Randomly select one of the returned photos to reduce duplicates
                var rand = new Random();
                var photo = result.Results[rand.Next(result.Results.Count)];
                var imageUrl = photo.Urls?.Regular ?? photo.Urls?.Small ?? string.Empty;
                _logger.LogInformation("Selected Unsplash image for {Destination} by {Photographer}. URL: {ImageUrl}", destination, photo.User?.Name, imageUrl);

                return (imageUrl,
                        photo.User?.Name,
                        photo.User?.Links?.Html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Unsplash image for {Destination}", destination);
                return null;
            }
        }

        private string? TryGetGoogleMapsStaticImage(string destination)
        {
            if (string.IsNullOrEmpty(_googleMapsApiKey))
            {
                _logger.LogDebug("Google Maps API key not configured");
                return null;
            }

            // Google Maps Static API - Street View
            var location = Uri.EscapeDataString(destination);
            return $"https://maps.googleapis.com/maps/api/streetview?size=800x400&location={location}&key={_googleMapsApiKey}";
        }

        private async Task CacheImageAsync(string normalizedDestination, string imageUrl, string source, string? photographerName, string? photographerUrl)
        {
            await _cacheSemaphore.WaitAsync();
            try
            {
                // Use a fresh DbContext for isolated write operation
                var existing = await _db.DestinationImages
                    .FirstOrDefaultAsync(di => di.Destination == normalizedDestination);

                if (existing != null)
                {
                    existing.ImageUrl = imageUrl;
                    existing.Source = source;
                    existing.PhotographerName = photographerName;
                    existing.PhotographerUrl = photographerUrl;
                    existing.CachedAt = DateTime.UtcNow;
                    existing.UsageCount++;
                }
                else
                {
                    var cachedImage = new DestinationImage
                    {
                        Destination = normalizedDestination,
                        ImageUrl = imageUrl,
                        Source = source,
                        PhotographerName = photographerName,
                        PhotographerUrl = photographerUrl,
                        CachedAt = DateTime.UtcNow,
                        UsageCount = 1
                    };
                    _db.DestinationImages.Add(cachedImage);
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("Cached {Source} image for {Destination}", source, normalizedDestination);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching image for {Destination}", normalizedDestination);
            }
            finally
            {
                _cacheSemaphore.Release();
            }
        }

        private static string NormalizeDestination(string destination)
        {
            return destination.Trim().ToLowerInvariant();
        }

        private static string CleanQuery(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            // Remove emoji & symbol categories by filtering to letters, digits, space
            var filteredChars = raw.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray();
            var filtered = new string(filteredChars);

            // Collapse multiple spaces
            while (filtered.Contains("  ")) filtered = filtered.Replace("  ", " ");

            filtered = filtered.Trim();

            // Remove very common filler prefixes (case-insensitive) that leak prompt text
            var prefixes = new[] {
                "the iconic", "the best time to visit", "a practical tip", "museum dedicated", "museum showcasing", "military museum",
                "street", "info", "information", "details" };
            foreach (var p in prefixes)
            {
                if (filtered.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                {
                    filtered = filtered[p.Length..].Trim();
                    break;
                }
            }

            // Limit length to first 6 words to keep query focused
            var words = filtered.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 6)
            {
                filtered = string.Join(' ', words.Take(6));
            }

            // If becomes empty fallback to original short trimmed
            if (string.IsNullOrWhiteSpace(filtered)) filtered = raw.Trim();
            return filtered;
        }

        // Unsplash API response models
        private class UnsplashSearchResponse
        {
            [JsonPropertyName("results")]
            public List<UnsplashPhoto> Results { get; set; } = new();
        }

        private class UnsplashPhoto
        {
            [JsonPropertyName("urls")]
            public UnsplashUrls? Urls { get; set; }

            [JsonPropertyName("user")]
            public UnsplashUser? User { get; set; }
        }

        private class UnsplashUrls
        {
            [JsonPropertyName("regular")]
            public string? Regular { get; set; }

            [JsonPropertyName("small")]
            public string? Small { get; set; }

            [JsonPropertyName("full")]
            public string? Full { get; set; }
        }

        private class UnsplashUser
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("links")]
            public UnsplashUserLinks? Links { get; set; }
        }

        private class UnsplashUserLinks
        {
            [JsonPropertyName("html")]
            public string? Html { get; set; }
        }

        /// <summary>
        /// Fetch multiple images sequentially (prevents DbContext concurrency exceptions)
        /// </summary>
        public async Task<Dictionary<string, string>> GetMultipleImagesAsync(IEnumerable<string> queries)
        {
            var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (queries == null || !queries.Any())
            {
                return results;
            }

            // Process images sequentially to avoid DbContext concurrency issues
            foreach (var query in queries.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var url = await GetDestinationImageAsync(query);
                    if (!string.IsNullOrEmpty(url))
                    {
                        results[query] = url;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch image for query: {Query}", query);
                }
            }

            _logger.LogInformation("Fetched {Count} images out of {Total} requested", results.Count, queries.Count());
            return results;
        }
    }
}
