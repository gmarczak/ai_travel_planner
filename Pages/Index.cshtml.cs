using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using project.Models;
using System.Linq;

namespace project.Pages;

public class IndexModel : PageModel
{
    private readonly IMemoryCache _cache;
    private const string SavedPlansKey = "SavedPlansList";

    public List<string> TopDestinations { get; private set; } = new();

    public IndexModel(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void OnGet()
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
    }
}