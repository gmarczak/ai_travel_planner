using project.Models;

namespace project.Services
{
    /// <summary>
    /// Interface for AI service providers (OpenAI, Claude, OpenRouter, Mistral, etc.)
    /// </summary>
    public interface IAiService
    {
        /// <summary>
        /// Generates a list of places for a destination
        /// </summary>
        Task<PlaceListResponse?> GeneratePlaceListAsync(TravelPlanRequest request);

        /// <summary>
        /// Generates detailed information for a specific place
        /// </summary>
        Task<string?> GeneratePlaceDetailsAsync(PlaceInfo place, TravelPlanRequest request);

        /// <summary>
        /// Generates a complete travel plan
        /// </summary>
        Task<TravelPlan> GenerateTravelPlanAsync(TravelPlanRequest request);

        /// <summary>
        /// Gets destination suggestions based on query
        /// </summary>
        Task<List<TravelSuggestion>> GetDestinationSuggestionsAsync(string query);

        /// <summary>
        /// Returns the name of the AI provider (e.g., "OpenAI", "Claude", "OpenRouter")
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Checks if the provider is available and configured
        /// </summary>
        Task<bool> IsAvailableAsync();
    }
}
