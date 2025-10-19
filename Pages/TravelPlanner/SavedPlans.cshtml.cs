using Microsoft.AspNetCore.Mvc.RazorPages;
using project.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;

namespace project.Pages.TravelPlanner
{
    public class SavedPlansModel : PageModel
    {
        private readonly IMemoryCache _cache;
        private const string SavedPlansKey = "SavedPlansList";
        public List<(string Id, TravelPlan Plan)> SavedPlans { get; set; } = new();

        public SavedPlansModel(IMemoryCache cache)
        {
            _cache = cache;
        }

        public void OnGet()
        {
            var ids = _cache.GetOrCreate(SavedPlansKey, entry => new List<string>());
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
