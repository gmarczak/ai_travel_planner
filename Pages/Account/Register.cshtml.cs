using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using project.Models;
using System.ComponentModel.DataAnnotations;

namespace project.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<RegisterModel> _logger;
    private readonly project.Services.SavedPlansService _savedPlansService;

    public RegisterModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ILogger<RegisterModel> logger, project.Services.SavedPlansService savedPlansService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _savedPlansService = savedPlansService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Full Name (optional)")]
        public string? FullName { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (ModelState.IsValid)
        {
            var user = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                FullName = Input.FullName,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║              🎉 NEW USER REGISTERED                        ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
                Console.WriteLine($"║ Email:      {user.Email,-45} ║");
                Console.WriteLine($"║ User ID:    {user.Id,-45} ║");
                Console.WriteLine($"║ Full Name:  {(user.FullName ?? "N/A"),-45} ║");
                Console.WriteLine($"║ Created:    {user.CreatedAt:yyyy-MM-dd HH:mm:ss}                         ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

                _logger.LogInformation("User {Email} created a new account with ID {UserId}.", user.Email, user.Id);

                await _signInManager.SignInAsync(user, isPersistent: false);

                // Merge anonymous saved plans into the new user account (if any)
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
                    _logger.LogWarning(ex, "Failed to merge anonymous plans for new user {UserId}", user.Id);
                }

                Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║           ✅ AUTO-LOGIN AFTER REGISTRATION                 ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
                Console.WriteLine($"║ Email:      {user.Email,-45} ║");
                Console.WriteLine($"║ Time:       {DateTime.Now:yyyy-MM-dd HH:mm:ss}                         ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

                return LocalRedirect(returnUrl);
            }
            else
            {
                Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║               ❌ REGISTRATION FAILED                       ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
                Console.WriteLine($"║ Email:      {Input.Email,-45} ║");
                Console.WriteLine($"║ Time:       {DateTime.Now:yyyy-MM-dd HH:mm:ss}                         ║");
                Console.WriteLine("║ Errors:                                                    ║");

                foreach (var error in result.Errors)
                {
                    var errorMsg = error.Description.Length > 54 ? error.Description.Substring(0, 51) + "..." : error.Description;
                    Console.WriteLine($"║   - {errorMsg,-54} ║");
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            }
        }

        return Page();
    }
}
