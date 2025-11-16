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

        // Use cache for pre-loaded images with 1-hour expiration
        const string imagesCacheKey = "HomePage_DestinationImages";
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
                    return (key, imageUrl ?? "https://images.unsplash.com/photo-1488646953014-85cb44e25828?w=800");
                }
                catch
                {
                    return (key, "https://images.unsplash.com/photo-1488646953014-85cb44e25828?w=800");
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