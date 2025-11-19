using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using project.Models;
using project.Data;

public class AdminPlansModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public List<TravelPlan> Plans { get; set; } = new();
    public bool IsAdmin { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 50;
    public int PageNumber { get; set; } = 1;

    public AdminPlansModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> OnGetAsync(int page = 1)
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

        PageNumber = page > 0 ? page : 1;
        TotalCount = await _db.TravelPlans.CountAsync();

        // Paginate and load only recent plans with minimal data
        Plans = await _db.TravelPlans
            .Include(p => p.User)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .AsNoTracking() // Read-only query optimization
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int planId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.IsAdmin != true)
        {
            return Forbid();
        }

        var plan = await _db.TravelPlans.FindAsync(planId);
        if (plan != null)
        {
            _db.TravelPlans.Remove(plan);
            await _db.SaveChangesAsync();
            TempData["Message"] = $"Travel plan #{planId} has been deleted.";
        }

        return RedirectToPage();
    }
}
