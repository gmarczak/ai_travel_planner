using project.Data;
using project.Models;
using Microsoft.Extensions.Caching.Memory;

namespace project.Services
{
    public class SavedPlansService
    {
        private readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _db;

        public SavedPlansService(IMemoryCache cache, ApplicationDbContext db)
        {
            _cache = cache;
            _db = db;
        }

        // Merge anonymous saved plans from cache (key: savedplans:anon:{anonId}) into DB for given userId
        public void MergeAnonymousPlansToUser(string userId, string anonId)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(anonId)) return;

            var key = $"savedplans:anon:{anonId}";
            var ids = _cache.GetOrCreate(key, entry => new List<string>());
            if (ids == null || ids.Count == 0) return;

            foreach (var id in ids)
            {
                try
                {
                    if (!_cache.TryGetValue(id, out TravelPlan? plan) || plan == null) continue;

                    // Check duplicate
                    var exists = _db.TravelPlans.Any(tp => tp.ExternalId == id && tp.UserId == userId);
                    if (exists) continue;

                    var entity = new TravelPlan
                    {
                        Destination = plan.Destination,
                        StartDate = plan.StartDate,
                        EndDate = plan.EndDate,
                        NumberOfTravelers = plan.NumberOfTravelers,
                        Budget = plan.Budget,
                        TravelPreferences = plan.TravelPreferences ?? string.Empty,
                        GeneratedItinerary = plan.GeneratedItinerary ?? string.Empty,
                        CreatedAt = DateTime.UtcNow,
                        UserId = userId,
                        ExternalId = id,
                        Accommodations = plan.Accommodations,
                        Activities = plan.Activities,
                        Transportation = plan.Transportation
                    };

                    _db.TravelPlans.Add(entity);
                }
                catch { /* swallow per-item errors */ }
            }

            _db.SaveChanges();

            // Clear anonymous list after merging
            _cache.Remove(key);
        }
    }
}
