using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using project.Data;
using Microsoft.AspNetCore.Identity;
using project.Models;
using Microsoft.Extensions.Caching.Memory;

public class AdminIndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMemoryCache _cache;

    public int UserCount { get; set; }
    public int PlanCount { get; set; }
    public int CacheCount { get; set; }
    public int ImageCount { get; set; }
    public bool IsAdmin { get; set; }

    public AdminIndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IMemoryCache cache)
    {
        _db = db;
        _userManager = userManager;
        _cache = cache;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Page(); // Handled in Razor
        }

        var user = await _userManager.GetUserAsync(User);
        IsAdmin = user?.IsAdmin ?? false;
        if (!IsAdmin)
        {
            return Page(); // Render forbidden section
        }

        // Cache dashboard stats for 5 minutes to reduce DB load
        const string cacheKey = "AdminDashboard_Stats";
        var stats = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            entry.Priority = CacheItemPriority.Normal;

            return new
            {
                Users = await _db.Users.CountAsync(),
                Plans = await _db.TravelPlans.CountAsync(),
                Cache = await _db.AiResponseCaches.CountAsync(),
                Images = await _db.DestinationImages.CountAsync()
            };
        });

        if (stats != null)
        {
            UserCount = stats.Users;
            PlanCount = stats.Plans;
            CacheCount = stats.Cache;
            ImageCount = stats.Images;
        }

        return Page();
    }
}
