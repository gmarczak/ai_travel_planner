using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace project.Pages
{
    public class SetLanguageModel : PageModel
    {
        public IActionResult OnGet(string culture, string returnUrl = "/")
        {
            var supportedCultures = new[] { "en", "pl" };

            if (string.IsNullOrWhiteSpace(culture) || !supportedCultures.Contains(culture))
            {
                culture = "en";
            }

            var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                cookieValue,
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    HttpOnly = false
                }
            );

            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                returnUrl = "/";
            }

            return LocalRedirect(returnUrl);
        }

        public IActionResult OnPost(string culture, string? returnUrl = null)
        {
            var supportedCultures = new[] { "en", "pl" };

            if (string.IsNullOrWhiteSpace(culture) || !supportedCultures.Contains(culture))
            {
                culture = "en";
            }

            var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                cookieValue,
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, HttpOnly = false }
            );

            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                returnUrl = "/";
            }

            return LocalRedirect(returnUrl);
        }
    }
}
