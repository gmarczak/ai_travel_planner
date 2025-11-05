using project.Models;

namespace project.Services
{
    /// <summary>
    /// Fallback AI service that tries multiple providers in order
    /// </summary>
    public class FallbackAiService : ITravelService
    {
        private readonly IEnumerable<IAiService> _aiProviders;
        private readonly ILogger<FallbackAiService> _logger;

        public FallbackAiService(
            IEnumerable<IAiService> aiProviders,
            ILogger<FallbackAiService> logger)
        {
            _aiProviders = aiProviders ?? throw new ArgumentNullException(nameof(aiProviders));
            _logger = logger;
        }

        public async Task<TravelPlan> GenerateTravelPlanAsync(TravelPlanRequest request)
        {
            var exceptions = new List<Exception>();

            foreach (var provider in _aiProviders)
            {
                try
                {
                    _logger.LogInformation("Attempting to generate travel plan using {Provider}", provider.ProviderName);

                    // Check if provider is available
                    if (!await provider.IsAvailableAsync())
                    {
                        _logger.LogWarning("{Provider} is not available, trying next provider", provider.ProviderName);
                        continue;
                    }

                    var plan = await provider.GenerateTravelPlanAsync(request);
                    _logger.LogInformation("Successfully generated travel plan using {Provider}", provider.ProviderName);
                    return plan;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating travel plan with {Provider}", provider.ProviderName);
                    exceptions.Add(ex);
                }
            }

            // If all providers failed, throw aggregate exception
            _logger.LogError("All AI providers failed to generate travel plan");
            throw new AggregateException("All AI providers failed", exceptions);
        }

        public async Task<List<TravelSuggestion>> GetDestinationSuggestionsAsync(string query)
        {
            var exceptions = new List<Exception>();

            foreach (var provider in _aiProviders)
            {
                try
                {
                    _logger.LogInformation("Attempting to get destination suggestions using {Provider}", provider.ProviderName);

                    if (!await provider.IsAvailableAsync())
                    {
                        _logger.LogWarning("{Provider} is not available, trying next provider", provider.ProviderName);
                        continue;
                    }

                    var suggestions = await provider.GetDestinationSuggestionsAsync(query);
                    _logger.LogInformation("Successfully got destination suggestions using {Provider}", provider.ProviderName);
                    return suggestions;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting destination suggestions with {Provider}", provider.ProviderName);
                    exceptions.Add(ex);
                }
            }

            // If all providers failed, return empty list
            _logger.LogWarning("All AI providers failed to get destination suggestions, returning empty list");
            return new List<TravelSuggestion>();
        }
    }
}
