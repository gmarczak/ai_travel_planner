using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using project.Models;
using project.Services;
using System.Linq;

namespace project.Pages;

public class IndexModel : PageModel
{
    private readonly IMemoryCache _cache;
    private readonly IImageService _imageService;
    private const string SavedPlansKey = "SavedPlansList";

    public List<string> TopDestinations { get; private set; } = new();
    public Dictionary<string, string> DestinationImages { get; private set; } = new();

    public IndexModel(IMemoryCache cache, IImageService imageService)
    {
        _cache = cache;
        _imageService = imageService;
    }

    public async Task OnGetAsync()
    {
        // READ SAVED PLANS FROM CACHE AND COMPUTE TOP DESTINATIONS
        var ids = _cache.GetOrCreate(SavedPlansKey, entry => new List<string>()) ?? new List<string>();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
        {
            if (_cache.TryGetValue(id, out TravelPlan? plan) && plan != null)
            {
                var dest = plan.Destination?.Trim();
                if (!string.IsNullOrEmpty(dest))
                {
                    if (counts.TryGetValue(dest, out var c)) counts[dest] = c + 1;
                    else counts[dest] = 1;
                }
            }
        }

        TopDestinations = counts.OrderByDescending(kv => kv.Value).Take(6).Select(kv => kv.Key).ToList();

        // Load all images in parallel (destinations + features) with caching
        var allImageKeys = new[]
        {
            "paris", "tokyo", "new york", "barcelona", "rome", "london",
            "artificial intelligence technology", "map navigation gps", "hotel restaurant dining"
        };

        // High-quality fallback URLs for each destination
        var fallbackUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"paris", "https://images.unsplash.com/photo-1502602898657-3e91760cbb34?w=800&auto=format&fit=crop"},
            {"tokyo", "https://images.unsplash.com/photo-1540959733332-eab4deabeeaf?w=800&auto=format&fit=crop"},
            {"new york", "https://images.unsplash.com/photo-1496442226666-8d4d0e62e6e9?w=800&auto=format&fit=crop"},
            {"barcelona", "https://images.unsplash.com/photo-1583422409516-2895a77efded?w=800&auto=format&fit=crop"},
            {"rome", "https://images.unsplash.com/photo-1552832230-c0197dd311b5?w=800&auto=format&fit=crop"},
            {"london", "https://images.unsplash.com/photo-1513635269975-59663e0ac1ad?w=800&auto=format&fit=crop"},
            {"artificial intelligence technology", "https://images.unsplash.com/photo-1677442136019-21780ecad995?w=800&auto=format&fit=crop"},
            {"map navigation gps", "https://images.unsplash.com/photo-1569336415962-a4bd9f69cd83?w=800&auto=format&fit=crop"},
            {"hotel restaurant dining", "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?w=800&auto=format&fit=crop"}
        };

        // Use cache for pre-loaded images with 1-hour expiration
        const string imagesCacheKey = "HomePage_DestinationImages_v2"; // Changed key to force refresh
        DestinationImages = await _cache.GetOrCreateAsync(imagesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            entry.Priority = CacheItemPriority.High;

            var images = new Dictionary<string, string>();
            var tasks = allImageKeys.Select(async key =>
            {
                try
                {
                    var imageUrl = await _imageService.GetDestinationImageAsync(key);
                    // Use fetched image if valid, otherwise use fallback
                    if (!string.IsNullOrWhiteSpace(imageUrl) && imageUrl.StartsWith("http"))
                    {
                        return (key, imageUrl);
                    }
                    return (key, fallbackUrls.GetValueOrDefault(key, "https://images.unsplash.com/photo-1488646953014-85cb44e25828?w=800&auto=format&fit=crop"));
                }
                catch
                {
                    return (key, fallbackUrls.GetValueOrDefault(key, "https://images.unsplash.com/photo-1488646953014-85cb44e25828?w=800&auto=format&fit=crop"));
                }
            });

            var results = await Task.WhenAll(tasks);
            foreach (var (key, url) in results)
            {
                images[key] = url;
            }

            return images;
        }) ?? new Dictionary<string, string>();
    }
}