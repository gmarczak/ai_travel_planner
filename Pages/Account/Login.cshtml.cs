using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using project.Models;
using System.ComponentModel.DataAnnotations;

namespace project.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
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
            var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(Input.Email);
                Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                  ✅ USER LOGGED IN                         ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
                Console.WriteLine($"║ Email:      {Input.Email,-45} ║");
                Console.WriteLine($"║ User ID:    {user?.Id,-45} ║");
                Console.WriteLine($"║ Full Name:  {(user?.FullName ?? "N/A"),-45} ║");
                Console.WriteLine($"║ Time:       {DateTime.Now:yyyy-MM-dd HH:mm:ss}                         ║");
                Console.WriteLine($"║ Remember:   {(Input.RememberMe ? "Yes" : "No"),-45} ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

                _logger.LogInformation("User {Email} logged in at {Time}.", Input.Email, DateTime.Now);
                return LocalRedirect(returnUrl);
            }
            else
            {
                Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                  ❌ LOGIN FAILED                           ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
                Console.WriteLine($"║ Email:      {Input.Email,-45} ║");
                Console.WriteLine($"║ Time:       {DateTime.Now:yyyy-MM-dd HH:mm:ss}                         ║");
                Console.WriteLine("║ Reason:     Invalid credentials                            ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }
        }

        return Page();
    }
}
