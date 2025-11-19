using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using project.Models;
using System.ComponentModel.DataAnnotations;

namespace project.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _user_manager;
    private readonly ILogger<LoginModel> _logger;
    private readonly project.Services.SavedPlansService _savedPlansService;

    public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ILogger<LoginModel> logger, project.Services.SavedPlansService savedPlansService)
    {
        _signInManager = signInManager;
        _user_manager = userManager;
        _logger = logger;
        _savedPlansService = savedPlansService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (ModelState.IsValid)
        {
            // Allow signing in with either Email or Username
            var user = await _user_manager.FindByEmailAsync(Input.Email)
                       ?? await _user_manager.FindByNameAsync(Input.Email);

            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(user.UserName!, Input.Password, Input.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
                    Console.WriteLine("║                  ✅ USER LOGGED IN                         ║");
                    Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
                    Console.WriteLine($"║ Email:      {(user.Email ?? Input.Email),-45} ║");
                    Console.WriteLine($"║ User ID:    {user.Id,-45} ║");
                    Console.WriteLine($"║ Full Name:  {(user.FullName ?? "N/A"),-45} ║");
                    Console.WriteLine($"║ Time:       {DateTime.Now:yyyy-MM-dd HH:mm:ss}                         ║");
                    Console.WriteLine($"║ Remember:   {(Input.RememberMe ? "Yes" : "No"),-45} ║");
                    Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

                    // Merge anonymous saved plans into user's account if present
                    try
                    {
                        var anonId = Request.Cookies["anon_saved_plans_id"];
                        if (!string.IsNullOrWhiteSpace(anonId))
                        {
                            _savedPlansService.MergeAnonymousPlansToUser(user.Id, anonId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to merge anonymous plans for user {Email}", user.Email ?? Input.Email);
                    }

                    _logger.LogInformation("User {Email} logged in at {Time}.", user.Email ?? Input.Email, DateTime.Now);
                    TempData["SuccessMessage"] = $"Welcome back, {user.FullName ?? user.Email ?? "User"}!";
                    return LocalRedirect(returnUrl);
                }

                // Invalid password or locked out
                Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                  ❌ LOGIN FAILED                           ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
                Console.WriteLine($"║ Identifier: {Input.Email,-45} ║");
                Console.WriteLine($"║ Time:       {DateTime.Now:yyyy-MM-dd HH:mm:ss}                         ║");
                Console.WriteLine("║ Reason:     Invalid credentials                            ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            // User not found
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                  ❌ LOGIN FAILED                           ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Identifier: {Input.Email,-45} ║");
            Console.WriteLine($"║ Time:       {DateTime.Now:yyyy-MM-dd HH:mm:ss}                         ║");
            Console.WriteLine("║ Reason:     User not found                                  ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return Page();
        }

        return Page();
    }
}
