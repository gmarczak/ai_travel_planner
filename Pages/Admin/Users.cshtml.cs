using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using project.Models;
using project.Data;

public class AdminUsersModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public List<ApplicationUser> Users { get; set; } = new();
    public bool IsAdmin { get; set; }

    public AdminUsersModel(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
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

        // Load recent users with read-only optimization (limit to 100 most recent)
        Users = await _db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Take(100)
            .AsNoTracking()
            .ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostMakeAdminAsync(string userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.IsAdmin != true)
        {
            return Forbid();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            user.IsAdmin = true;
            await _db.SaveChangesAsync();
            TempData["Message"] = $"User {user.UserName} is now an admin.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAdminAsync(string userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.IsAdmin != true)
        {
            return Forbid();
        }

        if (currentUser.Id == userId)
        {
            TempData["Error"] = "You cannot remove your own admin rights.";
            return RedirectToPage();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            user.IsAdmin = false;
            await _db.SaveChangesAsync();
            TempData["Message"] = $"Admin rights removed from {user.UserName}.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLockAsync(string userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.IsAdmin != true)
        {
            return Forbid();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            TempData["Message"] = $"User {user.UserName} has been locked.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUnlockAsync(string userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.IsAdmin != true)
        {
            return Forbid();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            TempData["Message"] = $"User {user.UserName} has been unlocked.";
        }

        return RedirectToPage();
    }
}
