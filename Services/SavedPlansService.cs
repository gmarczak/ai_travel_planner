using project.Data;
using project.Models;

namespace project.Services
{
    public class SavedPlansService
    {
        private readonly ApplicationDbContext _db;

        public SavedPlansService(ApplicationDbContext db)
        {
            _db = db;
        }

        // Merge anonymous saved plans into user account (simple UPDATE in DB)
        public void MergeAnonymousPlansToUser(string userId, string anonCookieId)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(anonCookieId)) return;

            // Find all plans with matching AnonymousCookieId
            var anonPlans = _db.TravelPlans
                .Where(tp => tp.AnonymousCookieId == anonCookieId && tp.UserId == null)
                .ToList();

            if (anonPlans.Count == 0) return;

            // Update to assign UserId and clear AnonymousCookieId
            foreach (var plan in anonPlans)
            {
                // Check if user already has this plan (by ExternalId)
                var exists = _db.TravelPlans.Any(tp => tp.ExternalId == plan.ExternalId && tp.UserId == userId);
                if (!exists)
                {
                    plan.UserId = userId;
                    plan.AnonymousCookieId = null;
                }
                else
                {
                    // Duplicate - remove the anonymous version
                    _db.TravelPlans.Remove(plan);
                }
            }

            _db.SaveChanges();
        }
    }
}
