using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using project.Models;
using project.Data;

public class AdminMonitoringModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public bool IsAdmin { get; set; }
    
    // Plan Generation Statistics
    public int TotalGenerationStates { get; set; }
    public int CompletedStates { get; set; }
    public int InProgressStates { get; set; }
    public int FailedStates { get; set; }
    
    // Cache Statistics
    public int CacheEntryCount { get; set; }
    public int TotalCacheHits { get; set; }
    public int TotalTokensCached { get; set; }
    public int ExpiredCacheCount { get; set; }
    
    // Image Cache Statistics
    public int ImageCacheCount { get; set; }
    public DateTime? MostRecentImageCache { get; set; }
    
    // System Statistics
    public int TotalUsers { get; set; }
    public int TotalPlans { get; set; }
    
    // Recent Failures
    public List<PlanGenerationState> RecentFailures { get; set; } = new();

    public AdminMonitoringModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Page();
        }

        var currentUser = await _userManager.GetUserAsync(User);
        IsAdmin = currentUser?.IsAdmin ?? false;
        
        if (!IsAdmin)
        {
            return Page();
        }

        // Plan Generation Stats (parallel queries)
        var statsTask = Task.WhenAll(
            _db.PlanGenerationStates.CountAsync(),
            _db.PlanGenerationStates.CountAsync(s => s.Status == PlanGenerationStatus.Completed),
            _db.PlanGenerationStates.CountAsync(s => 
                s.Status == PlanGenerationStatus.InProgress || 
                s.Status == PlanGenerationStatus.Queued),
            _db.PlanGenerationStates.CountAsync(s => s.Status == PlanGenerationStatus.Failed)
        );

        // Cache Stats (aggregate in database, not in memory)
        var now = DateTime.UtcNow;
        var cacheStatsTask = Task.WhenAll(
            _db.AiResponseCaches.CountAsync(),
            _db.AiResponseCaches.SumAsync(c => c.HitCount),
            _db.AiResponseCaches.SumAsync(c => c.TokenCount),
            _db.AiResponseCaches.CountAsync(c => c.ExpiresAt.HasValue && c.ExpiresAt < now)
        );
        
        // Image Cache Stats
        var imageCountTask = _db.DestinationImages.CountAsync();
        var imageMostRecentTask = _db.DestinationImages
            .OrderByDescending(i => i.CachedAt)
            .Select(i => (DateTime?)i.CachedAt)
            .FirstOrDefaultAsync();

        // System Stats
        var systemStatsTask = Task.WhenAll(
            _db.Users.CountAsync(),
            _db.TravelPlans.CountAsync()
        );

        // Recent Failures
        var recentFailuresTask = _db.PlanGenerationStates
            .Where(s => s.Status == PlanGenerationStatus.Failed)
            .OrderByDescending(s => s.LastUpdatedAt)
            .Take(10)
            .AsNoTracking()
            .ToListAsync();

        // Wait for all queries to complete in parallel
        await Task.WhenAll(statsTask, cacheStatsTask, imageCountTask, imageMostRecentTask, systemStatsTask, recentFailuresTask);

        var stats = await statsTask;
        TotalGenerationStates = stats[0];
        CompletedStates = stats[1];
        InProgressStates = stats[2];
        FailedStates = stats[3];

        var cacheStats = await cacheStatsTask;
        CacheEntryCount = cacheStats[0];
        TotalCacheHits = cacheStats[1];
        TotalTokensCached = cacheStats[2];
        ExpiredCacheCount = cacheStats[3];

        ImageCacheCount = await imageCountTask;
        MostRecentImageCache = await imageMostRecentTask;

        var systemStats = await systemStatsTask;
        TotalUsers = systemStats[0];
        TotalPlans = systemStats[1];

        RecentFailures = await recentFailuresTask;
        
        return Page();
    }
}
