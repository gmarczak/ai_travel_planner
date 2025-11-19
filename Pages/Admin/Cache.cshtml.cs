using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using project.Models;
using project.Data;

public class AdminCacheModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public List<AiResponseCache> CacheEntries { get; set; } = new();
    public bool IsAdmin { get; set; }

    public AdminCacheModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
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

        CacheEntries = await _db.AiResponseCaches
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int cacheId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.IsAdmin != true)
        {
            return Forbid();
        }

        var cache = await _db.AiResponseCaches.FindAsync(cacheId);
        if (cache != null)
        {
            _db.AiResponseCaches.Remove(cache);
            await _db.SaveChangesAsync();
            TempData["Message"] = $"Cache entry #{cacheId} has been deleted.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClearExpiredAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.IsAdmin != true)
        {
            return Forbid();
        }

        var expired = await _db.AiResponseCaches
            .Where(c => c.ExpiresAt.HasValue && c.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        _db.AiResponseCaches.RemoveRange(expired);
        await _db.SaveChangesAsync();

        TempData["Message"] = $"Deleted {expired.Count} expired cache entries.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClearAllAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.IsAdmin != true)
        {
            return Forbid();
        }

        var count = await _db.AiResponseCaches.CountAsync();
        _db.AiResponseCaches.RemoveRange(_db.AiResponseCaches);
        await _db.SaveChangesAsync();

        TempData["Message"] = $"Deleted all {count} cache entries.";
        return RedirectToPage();
    }
}
