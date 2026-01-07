using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace project.Pages
{
    public class SetLanguageModel : PageModel
    {
        private readonly ILogger<SetLanguageModel> _logger;

        public SetLanguageModel(ILogger<SetLanguageModel> logger)
        {
            _logger = logger;
        }

        public IActionResult OnGet(string returnUrl = "/")
        {
            var culture = Request.Query["culture"].ToString();
            var supportedCultures = new[] { "en-US", "pl-PL" };

            culture = culture?.Trim()?.ToLowerInvariant();
            culture = culture switch
            {
                "pl" or "pl-pl" => "pl-PL",
                "en" or "en-us" => "en-US",
                _ => culture
            };

            if (string.IsNullOrWhiteSpace(culture) || !supportedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
            {
                culture = "en-US";
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

        public IActionResult OnPost(string? returnUrl = null)
        {
            // Read culture from form or query
            var culture = Request.Form["culture"].ToString() ?? Request.Query["culture"].ToString();
            _logger.LogInformation($"[SetLanguage.OnPost] Called, culture='{culture}', returnUrl='{returnUrl}'");

            var supportedCultures = new[] { "en-US", "pl-PL" };

            culture = culture?.Trim()?.ToLowerInvariant();
            culture = culture switch
            {
                "pl" or "pl-pl" => "pl-PL",
                "en" or "en-us" => "en-US",
                _ => culture
            };

            if (string.IsNullOrWhiteSpace(culture) || !supportedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
            {
                culture = "en-US";
            }

            var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
            _logger.LogInformation($"[SetLanguage.OnPost] Setting cookie with culture='{culture}'");

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
