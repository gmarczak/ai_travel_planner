using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using project.Data;
using Microsoft.AspNetCore.Identity;
using project.Models;

public class AdminIndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public int UserCount { get; set; }
    public int PlanCount { get; set; }
    public int CacheCount { get; set; }
    public int ImageCount { get; set; }
    public bool IsAdmin { get; set; }

    public AdminIndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
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

        UserCount = await _db.Users.CountAsync();
        PlanCount = await _db.TravelPlans.CountAsync();
        CacheCount = await _db.AiResponseCaches.CountAsync();
        ImageCount = await _db.DestinationImages.CountAsync();
        return Page();
    }
}
