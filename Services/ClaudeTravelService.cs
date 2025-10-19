using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using project.Models;

namespace project.Services
{
    public class ClaudeTravelService : ITravelService
    {
        private readonly ILogger<ClaudeTravelService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _http;

        private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
        private const string AnthropicVersion = "2023-06-01";

        public ClaudeTravelService(ILogger<ClaudeTravelService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _http = new HttpClient();
        }

        public async Task<TravelPlan> GenerateTravelPlanAsync(TravelPlanRequest request)
        {
            var apiKey = _configuration["Anthropic:ApiKey"]
                        ?? _configuration["ANTHROPIC_API_KEY"]
                        ?? _configuration["CLAUDE_API_KEY"];
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Anthropic API key is not configured. Set 'Anthropic:ApiKey' or 'ANTHROPIC_API_KEY'.");

            var model = _configuration["Anthropic:Model"] ?? "claude-3-5-sonnet-20241022";

            var days = (request.EndDate - request.StartDate).Days + 1;
            var preferences = string.IsNullOrWhiteSpace(request.TravelPreferences) ? "None specified" : request.TravelPreferences;
            var tripType = string.IsNullOrWhiteSpace(request.TripType) ? "General" : request.TripType;

            // SYSTEM & USER PROMPTS
            var systemPrompt = "You are an expert travel planner. Create detailed, personalized travel itineraries. Always respond in JSON as specified.";
            var userPrompt = $@"Create a detailed {days}-day travel plan for {request.Destination} for {request.NumberOfTravelers} travelers with a budget of ${request.Budget}.

Trip details:
- Dates: {request.StartDate:MMM dd, yyyy} to {request.EndDate:MMM dd, yyyy}
- Travelers: {request.NumberOfTravelers}
- Budget: ${request.Budget}
- Trip type: {tripType}
- Additional preferences: {preferences}

Respond with a JSON object exactly like:
{{
  ""itinerary"": ""Detailed day-by-day itinerary as a single string with line breaks"",
  ""accommodations"": [""Hotel 1"", ""Hotel 2"", ""Hotel 3""],
  ""activities"": [""Activity 1"", ""Activity 2"", ""Activity 3"", ""Activity 4""],
  ""transportation"": [""Option 1"", ""Option 2"", ""Option 3""]
}}";

            var payload = new AnthropicMessageRequest
            {
                Model = model,
                MaxTokens = 1200,
                System = systemPrompt,
                Messages = new List<AnthropicMessage>
                {
                    new AnthropicMessage { Role = "user", Content = new List<AnthropicContent> { new AnthropicContent { Type = "text", Text = userPrompt } } }
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, AnthropicApiUrl);
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", AnthropicVersion);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                var res = await _http.SendAsync(req);
                res.EnsureSuccessStatusCode();
                var json = await res.Content.ReadAsStringAsync();
                _logger.LogInformation("Received response from Anthropic: {Response}", json);

                var parsed = JsonSerializer.Deserialize<AnthropicMessageResponse>(json);
                var text = parsed?.Content?.FirstOrDefault()?.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("Anthropic response had no text content.");
                    return CreateFallbackPlan(request);
                }

                // TRY PARSE JSON FROM ANTHROPIC
                TravelPlanResponse? aiResponse = null;
                try
                {
                    aiResponse = JsonSerializer.Deserialize<TravelPlanResponse>(text);
                }
                catch (JsonException jex)
                {
                    _logger.LogWarning(jex, "Failed to parse Anthropic text as JSON. Falling back to raw text.");
                }

                return new TravelPlan
                {
                    Id = new Random().Next(1, 10000),
                    Destination = request.Destination,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    NumberOfTravelers = request.NumberOfTravelers,
                    Budget = request.Budget,
                    TravelPreferences = request.TravelPreferences ?? string.Empty,
                    CreatedAt = DateTime.Now,
                    GeneratedItinerary = aiResponse?.Itinerary ?? text,
                    Accommodations = aiResponse?.Accommodations ?? GetFallbackAccommodations(request.Destination),
                    Activities = aiResponse?.Activities ?? GetFallbackActivities(request.Destination),
                    Transportation = aiResponse?.Transportation ?? GetFallbackTransportation()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Anthropic for destination {Destination}", request.Destination);
                return CreateFallbackPlan(request);
            }
        }

        public Task<List<TravelSuggestion>> GetDestinationSuggestionsAsync(string query)
        {
            // FALLBACK SUGGESTIONS
            var destinations = new List<string>
            {
                "Paris, France", "Tokyo, Japan", "New York, USA", "Rome, Italy",
                "Barcelona, Spain", "London, UK", "Amsterdam, Netherlands",
                "Prague, Czech Republic", "Istanbul, Turkey", "Bangkok, Thailand"
            };

            var list = destinations
                .Where(d => d.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(6)
                .Select(d => new TravelSuggestion
                {
                    Type = "Destination",
                    Name = d,
                    Description = "Popular travel destination",
                    Priority = 1
                })
                .ToList();
            return Task.FromResult(list);
        }

        private TravelPlan CreateFallbackPlan(TravelPlanRequest request)
        {
            var days = (request.EndDate - request.StartDate).Days + 1;
            var text = $"Day 1 ({request.StartDate:MMM dd}): Arrival in {request.Destination}. Explore local sights.\n" +
                       (days >= 2 ? $"Day 2 ({request.StartDate.AddDays(1):MMM dd}): Guided city tour and food experiences.\n" : string.Empty) +
                       "...";
            return new TravelPlan
            {
                Id = new Random().Next(1, 10000),
                Destination = request.Destination,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                NumberOfTravelers = request.NumberOfTravelers,
                Budget = request.Budget,
                TravelPreferences = request.TravelPreferences ?? string.Empty,
                CreatedAt = DateTime.Now,
                GeneratedItinerary = text,
                Accommodations = GetFallbackAccommodations(request.Destination),
                Activities = GetFallbackActivities(request.Destination),
                Transportation = GetFallbackTransportation()
            };
        }

        private List<string> GetFallbackAccommodations(string destination) => new()
        {
            $"Recommended Hotel in {destination}",
            $"Boutique Stay near {destination}",
            $"Budget Option in {destination}"
        };

        private List<string> GetFallbackActivities(string destination) => new()
        {
            $"City Tour of {destination}",
            $"Local Food Experience in {destination}",
            $"Historic Sites in {destination}"
        };

        private List<string> GetFallbackTransportation() => new()
        {
            "Airport Transfer",
            "Public Transport Pass",
            "Taxi / Rideshare"
        };

        // DTOs for Anthropic
        private class AnthropicMessageRequest
        {
            [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
            [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
            [JsonPropertyName("system")] public string System { get; set; } = string.Empty;
            [JsonPropertyName("messages")] public List<AnthropicMessage> Messages { get; set; } = new();
        }

        private class AnthropicMessage
        {
            [JsonPropertyName("role")] public string Role { get; set; } = "user";
            [JsonPropertyName("content")] public List<AnthropicContent> Content { get; set; } = new();
        }

        private class AnthropicContent
        {
            [JsonPropertyName("type")] public string Type { get; set; } = "text";
            [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
        }

        private class AnthropicMessageResponse
        {
            [JsonPropertyName("content")] public List<AnthropicContent> Content { get; set; } = new();
        }

        private class TravelPlanResponse
        {
            [JsonPropertyName("itinerary")] public string? Itinerary { get; set; }
            [JsonPropertyName("accommodations")] public List<string>? Accommodations { get; set; }
            [JsonPropertyName("activities")] public List<string>? Activities { get; set; }
            [JsonPropertyName("transportation")] public List<string>? Transportation { get; set; }
        }
    }
}
