using Microsoft.AspNetCore.Mvc.RazorPages;
using project.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Linq;

namespace project.Pages.TravelPlanner
{
    public class SavedPlansModel : PageModel
    {
        private readonly IMemoryCache _cache;
        private readonly project.Data.ApplicationDbContext _db;
        private const string SavedPlansKeyPrefix = "savedplans:";
        private const string AnonymousCookieName = "anon_saved_plans_id";
        public List<(string Id, TravelPlan Plan)> SavedPlans { get; set; } = new();

        public SavedPlansModel(IMemoryCache cache, project.Data.ApplicationDbContext db)
        {
            _cache = cache;
            _db = db;
        }

        private string GetSavedPlansKey()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(userId)) return SavedPlansKeyPrefix + "user:" + userId;
            }

            if (Request.Cookies.TryGetValue(AnonymousCookieName, out var existing) && !string.IsNullOrWhiteSpace(existing))
            {
                return SavedPlansKeyPrefix + "anon:" + existing;
            }

            return string.Empty;
        }

        public void OnGet()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    var plans = _db.TravelPlans.Where(tp => tp.UserId == userId).OrderByDescending(tp => tp.CreatedAt).ToList();
                    foreach (var p in plans)
                    {
                        var id = p.ExternalId ?? p.Id.ToString();
                        SavedPlans.Add((id, p));
                    }
                    return;
                }
            }

            var key = GetSavedPlansKey();
            if (string.IsNullOrWhiteSpace(key)) return;
            var ids = _cache.GetOrCreate(key, entry => new List<string>());
            if (ids == null || ids.Count == 0) return;
            foreach (var id in ids)
            {
                if (_cache.TryGetValue(id, out TravelPlan? plan) && plan != null)
                {
                    SavedPlans.Add((id, plan));
                }
            }
        }
    }
}
