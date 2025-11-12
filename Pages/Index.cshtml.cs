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

        // Load images for popular destinations
        var defaultDestinations = new[] { "paris", "tokyo", "new york", "barcelona", "rome", "london" };

        foreach (var destination in defaultDestinations)
        {
            try
            {
                var imageUrl = await _imageService.GetDestinationImageAsync(destination);
                DestinationImages[destination] = imageUrl ?? "https://images.unsplash.com/photo-1488646953014-85cb44e25828?w=800";
            }
            catch
            {
                DestinationImages[destination] = "https://images.unsplash.com/photo-1488646953014-85cb44e25828?w=800";
            }
        }

        // Load images for features showcase
        var featureKeywords = new[] { "artificial intelligence technology", "map navigation gps", "hotel restaurant dining" };

        foreach (var keyword in featureKeywords)
        {
            try
            {
                var imageUrl = await _imageService.GetDestinationImageAsync(keyword);
                DestinationImages[keyword] = imageUrl ?? "https://images.unsplash.com/photo-1488646953014-85cb44e25828?w=800";
            }
            catch
            {
                DestinationImages[keyword] = "https://images.unsplash.com/photo-1488646953014-85cb44e25828?w=800";
            }
        }
    }
}