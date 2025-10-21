using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using project.Models;
using System.Collections.Generic;
using System.Linq;

namespace project.Pages.TravelPlanner
{
    public class SavedPlansModel : PageModel
    {
        private readonly ILogger<SavedPlansModel> _logger;
        private readonly project.Data.ApplicationDbContext _db;
        private const string AnonymousCookieName = "anon_saved_plans_id";
        public List<(string Id, TravelPlan Plan)> SavedPlans { get; set; } = new();

        public SavedPlansModel(project.Data.ApplicationDbContext db, ILogger<SavedPlansModel> logger)
        {
            _db = db;
            _logger = logger;
        }

        public void OnGet()
        {
            // Query DB for saved plans (authenticated or anonymous)
            if (User?.Identity?.IsAuthenticated == true)
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    var plans = _db.TravelPlans
                        .Where(tp => tp.UserId == userId)
                        .OrderByDescending(tp => tp.CreatedAt)
                        .ToList();

                    foreach (var p in plans)
                    {
                        var id = p.ExternalId ?? p.Id.ToString();
                        SavedPlans.Add((id, p));
                    }
                    _logger.LogInformation("SavedPlans: loaded {Count} plans for userId={UserId}", plans.Count, userId);
                    return;
                }
            }

            // Anonymous user - query by AnonymousCookieId
            if (Request.Cookies.TryGetValue(AnonymousCookieName, out var anonCookieId) && !string.IsNullOrWhiteSpace(anonCookieId))
            {
                var plans = _db.TravelPlans
                    .Where(tp => tp.AnonymousCookieId == anonCookieId)
                    .OrderByDescending(tp => tp.CreatedAt)
                    .ToList();

                foreach (var p in plans)
                {
                    var id = p.ExternalId ?? p.Id.ToString();
                    SavedPlans.Add((id, p));
                }
                _logger.LogInformation("SavedPlans: loaded {Count} plans for anonId={AnonId}", plans.Count, anonCookieId);
            }
            else
            {
                _logger.LogInformation("SavedPlans: no anonymous cookie found");
            }
        }

        // POST handler to remove a saved plan from DB
        public IActionResult OnPostRemove(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "No plan id provided.";
                return RedirectToPage();
            }

            // Remove from DB (for both authenticated and anonymous users)
            var userId = User?.Identity?.IsAuthenticated == true
                ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                : null;

            var anonymousCookieId = string.IsNullOrWhiteSpace(userId) && Request.Cookies.TryGetValue(AnonymousCookieName, out var anonId)
                ? anonId
                : null;

            var existing = userId != null
                ? _db.TravelPlans.FirstOrDefault(tp => tp.ExternalId == id && tp.UserId == userId)
                : _db.TravelPlans.FirstOrDefault(tp => tp.ExternalId == id && tp.AnonymousCookieId == anonymousCookieId);

            if (existing != null)
            {
                _db.TravelPlans.Remove(existing);
                _db.SaveChanges();
                _logger.LogInformation("Plan removed from DB: id={Id}", id);
            }

            TempData["SuccessMessage"] = "Plan removed from saved.";
            return RedirectToPage();
        }
    }
}
