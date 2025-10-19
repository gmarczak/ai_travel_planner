using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using project.Models;

namespace project.Pages.Account;

[Authorize]
[ValidateAntiForgeryToken]
public class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(SignInManager<ApplicationUser> signInManager, ILogger<LogoutModel> logger)
    {
        _signInManager = signInManager;
        _logger = logger;
    }

    public async Task<IActionResult> OnPost(string? returnUrl = null)
    {
        var userEmail = User.Identity?.Name ?? "Unknown";
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown";

        await _signInManager.SignOutAsync();

        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                 👋 USER LOGGED OUT                         ║");
        Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ Email:      {userEmail,-45} ║");
        Console.WriteLine($"║ User ID:    {userId,-45} ║");
        Console.WriteLine($"║ Time:       {DateTime.Now:yyyy-MM-dd HH:mm:ss}                         ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

        _logger.LogInformation("User {Email} logged out at {Time}.", userEmail, DateTime.Now);

        if (returnUrl != null)
        {
            return LocalRedirect(returnUrl);
        }
        else
        {
            return RedirectToPage("/Index");
        }
    }
}
