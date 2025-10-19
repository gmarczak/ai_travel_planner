using project.Models;

namespace project.Services
{
    public interface ITravelService
    {
        Task<TravelPlan> GenerateTravelPlanAsync(TravelPlanRequest request);
        Task<List<TravelSuggestion>> GetDestinationSuggestionsAsync(string query);
    }
}