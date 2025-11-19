using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using project.Models;

namespace project.Services
{
    /// <summary>
    /// OpenRouter AI service - provides access to multiple AI models through OpenRouter API
    /// </summary>
    public class OpenRouterAiService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenRouterAiService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IAiCacheService _cacheService;
        private readonly JsonSerializerOptions _jsonOptions;

        public string ProviderName => "OpenRouter";

        public OpenRouterAiService(
            IHttpClientFactory httpClientFactory,
            ILogger<OpenRouterAiService> logger,
            IConfiguration configuration,
            IAiCacheService cacheService)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
            _configuration = configuration;
            _cacheService = cacheService;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public Task<bool> IsAvailableAsync()
        {
            var apiKey = GetApiKey();
            return Task.FromResult(!string.IsNullOrWhiteSpace(apiKey));
        }

        public async Task<PlaceListResponse?> GeneratePlaceListAsync(TravelPlanRequest request)
        {
            _logger.LogInformation("OpenRouter: Generating place list for {Destination}", request.Destination);

            var prompt = $@"You must respond with ONLY valid JSON. No additional text.

Task: Suggest the top places for a trip to {request.Destination}.

Required JSON format:
{{
  ""places"": [
    {{
      ""name"": ""Place name"",
      ""type"": ""type"",
      ""location"": ""location"",
      ""description"": ""description""
    }}
  ]
}}";

            var response = await CallOpenRouterAsync(prompt, "openai/gpt-3.5-turbo");

            if (string.IsNullOrEmpty(response))
                return null;

            try
            {
                var result = JsonSerializer.Deserialize<PlaceListResponse>(response, _jsonOptions);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenRouter: Failed to parse place list JSON");
                return null;
            }
        }

        public async Task<string?> GeneratePlaceDetailsAsync(PlaceInfo place, TravelPlanRequest request)
        {
            var prompt = $"Provide brief travel information for '{place.Name}' in {request.Destination}. Keep it concise (2-3 sentences).";
            return await CallOpenRouterAsync(prompt, "openai/gpt-3.5-turbo");
        }

        public async Task<TravelPlan> GenerateTravelPlanAsync(TravelPlanRequest request)
        {
            _logger.LogInformation("OpenRouter: Generating travel plan for {Destination}", request.Destination);

            // Use multi-step approach
            var placeList = await GeneratePlaceListAsync(request);

            if (placeList != null && placeList.Places.Any())
            {
                var detailsTasks = placeList.Places.Take(10).Select(place => GeneratePlaceDetailsAsync(place, request));
                var details = await Task.WhenAll(detailsTasks);

                var itineraryBuilder = new StringBuilder();
                itineraryBuilder.AppendLine($"🗺️ Travel Plan for {request.Destination}\n");

                for (int i = 0; i < placeList.Places.Count && i < details.Length; i++)
                {
                    var place = placeList.Places[i];
                    var detail = details[i];
                    itineraryBuilder.AppendLine($"📍 {place.Name}");
                    if (!string.IsNullOrWhiteSpace(detail))
                    {
                        itineraryBuilder.AppendLine($"   {detail}");
                    }
                    itineraryBuilder.AppendLine();
                }

                return new TravelPlan
                {
                    Id = new Random().Next(1, 10000),
                    Destination = request.Destination,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    NumberOfTravelers = request.NumberOfTravelers,
                    Budget = request.Budget ?? 0m,
                    TravelPreferences = request.TravelPreferences ?? "",
                    CreatedAt = DateTime.Now,
                    GeneratedItinerary = itineraryBuilder.ToString(),
                    Accommodations = new List<string> { $"Hotels in {request.Destination}" },
                    Activities = placeList.Places.Select(p => p.Name).ToList(),
                    Transportation = new List<string> { "Public transport", "Taxi services" }
                };
            }

            throw new InvalidOperationException("OpenRouter: Failed to generate travel plan");
        }

        public Task<List<TravelSuggestion>> GetDestinationSuggestionsAsync(string query)
        {
            _logger.LogInformation("OpenRouter: Getting destination suggestions for {Query}", query);

            var destinations = new List<string>
            {
                "Paris, France", "Tokyo, Japan", "New York, USA", "Rome, Italy",
                "Barcelona, Spain", "London, UK", "Amsterdam, Netherlands"
            };

            var results = destinations
                .Where(d => d.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .Select(d => new TravelSuggestion
                {
                    Type = "Destination",
                    Name = d,
                    Description = "Popular destination",
                    Priority = 1
                })
                .ToList();

            return Task.FromResult(results);
        }

        private async Task<string?> CallOpenRouterAsync(string prompt, string model)
        {
            var apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("OpenRouter API key is not configured");
                return null;
            }

            try
            {
                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.Add("HTTP-Referer", "https://travelplanner.app");
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(responseBody);

                return jsonDoc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenRouter: Error calling API");
                return null;
            }
        }

        private string? GetApiKey()
        {
            return _configuration["OpenRouter:ApiKey"]
                ?? _configuration["OPENROUTER_API_KEY"];
        }
    }
}
