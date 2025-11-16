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

        // Plan Generation Stats
        TotalGenerationStates = await _db.PlanGenerationStates.CountAsync();
        CompletedStates = await _db.PlanGenerationStates.CountAsync(s => s.Status == PlanGenerationStatus.Completed);
        InProgressStates = await _db.PlanGenerationStates.CountAsync(s => 
            s.Status == PlanGenerationStatus.InProgress || 
            s.Status == PlanGenerationStatus.Queued);
        FailedStates = await _db.PlanGenerationStates.CountAsync(s => s.Status == PlanGenerationStatus.Failed);
        
        // Cache Stats
        var cacheEntries = await _db.AiResponseCaches.ToListAsync();
        CacheEntryCount = cacheEntries.Count;
        TotalCacheHits = cacheEntries.Sum(c => c.HitCount);
        TotalTokensCached = cacheEntries.Sum(c => c.TokenCount);
        ExpiredCacheCount = cacheEntries.Count(c => c.ExpiresAt.HasValue && c.ExpiresAt < DateTime.UtcNow);
        
        // Image Cache Stats
        ImageCacheCount = await _db.DestinationImages.CountAsync();
        var mostRecent = await _db.DestinationImages
            .OrderByDescending(i => i.CachedAt)
            .FirstOrDefaultAsync();
        MostRecentImageCache = mostRecent?.CachedAt;
        
        // System Stats
        TotalUsers = await _db.Users.CountAsync();
        TotalPlans = await _db.TravelPlans.CountAsync();
        
        // Recent Failures
        RecentFailures = await _db.PlanGenerationStates
            .Where(s => s.Status == PlanGenerationStatus.Failed)
            .OrderByDescending(s => s.LastUpdatedAt)
            .Take(10)
            .ToListAsync();
        
        return Page();
    }
}
