using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Localization;

namespace project.Pages
{
    public class SetLanguageModel : PageModel
    {
        public IActionResult OnPost(string culture, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(culture)) culture = "en-US";
            var cookieValue = Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(new Microsoft.AspNetCore.Localization.RequestCulture(culture));
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                cookieValue,
                new Microsoft.AspNetCore.Http.CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true }
            );

            if (string.IsNullOrWhiteSpace(returnUrl)) returnUrl = "/";
            return LocalRedirect(returnUrl);
        }
    }
}
