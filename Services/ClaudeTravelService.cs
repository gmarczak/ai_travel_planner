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

        public ClaudeTravelService(ILogger<ClaudeTravelService> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _http = httpClientFactory.CreateClient();
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
            var systemPrompt = $"You are an expert travel planner. Create detailed, personalized travel itineraries. Always respond in JSON as specified. Respond in English (en-US).";
            var budgetValue = request.Budget ?? 0m;

            var transportInstructions = "";
            if (!string.IsNullOrWhiteSpace(request.TransportMode))
            {
                var departureInfo = !string.IsNullOrWhiteSpace(request.DepartureLocation)
                    ? $" from {request.DepartureLocation}"
                    : " from major international hubs";
                var departureDetail = !string.IsNullOrWhiteSpace(request.DepartureLocation)
                    ? $" Analyze best routes FROM {request.DepartureLocation} TO {request.Destination}."
                    : "";

                transportInstructions = request.TransportMode switch
                {
                    "Flight" => $"\n\n✈️ TRANSPORT MODE: Flying{departureInfo}\nIn the 'transportation' array, include:\n- Specific flight routes: {request.DepartureLocation ?? "major hubs"} → {request.Destination} (with nearby alternative airports for BOTH cities)\n- Estimated flight duration and typical costs for this specific route\n- Airport transfer options at destination (train, bus, taxi) with prices\n- Best booking websites and timing tips{departureDetail}",
                    "Car" => "\n\n🚗 TRANSPORT MODE: Driving\nIn the 'transportation' array, include:\n- Scenic driving routes and road trip highlights\n- Parking information and costs at major attractions\n- Car rental tips and estimated costs\n- Ferry routes if water crossing needed (e.g., Portsmouth-Santander for Spain) with booking websites",
                    "Train" => "\n\n🚆 TRANSPORT MODE: Train\nIn the 'transportation' array, include:\n- Main train stations and connections\n- Regional/national rail passes (Eurail, etc.) with prices\n- Recommended day trips accessible by train\n- Booking websites (Trainline, local railways)",
                    "Bus" => "\n\n🚌 TRANSPORT MODE: Bus\nIn the 'transportation' array, include:\n- Main bus terminals and international routes\n- FlixBus, Eurolines, or regional bus companies\n- Long-distance bus passes and prices\n- Booking websites and tips",
                    _ => ""
                };
            }

            var userPrompt = $@"Create a detailed {days}-day travel plan for {request.Destination} for {request.NumberOfTravelers} travelers with a budget of ${budgetValue}.

Trip details:
- Dates: {request.StartDate:MMM dd, yyyy} to {request.EndDate:MMM dd, yyyy}
- Travelers: {request.NumberOfTravelers}
- Budget: ${request.Budget}
- Trip type: {tripType}

CRITICAL USER PREFERENCES (must be incorporated throughout EVERY day):
{preferences}{transportInstructions}

Ensure all activities, restaurants, and experiences in the itinerary strongly reflect these preferences.

Respond with a JSON object. Return an object with these keys (names in English): 'itinerary' (the full day-by-day itinerary as a single string with line breaks), 'accommodations' (array of hotels), 'activities' (array of activities), 'transportation' (array of transport tips).";

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
                    Budget = request.Budget ?? 0m,
                    TravelPreferences = request.TravelPreferences ?? string.Empty,
                    TransportMode = request.TransportMode,
                    DepartureLocation = request.DepartureLocation,
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
                Budget = request.Budget ?? 0m,
                TravelPreferences = request.TravelPreferences ?? string.Empty,
                TransportMode = request.TransportMode,
                DepartureLocation = request.DepartureLocation,
                CreatedAt = DateTime.Now,
                GeneratedItinerary = text,
                Accommodations = GetFallbackAccommodations(request.Destination),
                Activities = GetFallbackActivities(request.Destination),
                Transportation = GetFallbackTransportation()
            };
        }

        private List<string> GetFallbackAccommodations(string destination)
            => TravelPlanFallbackHelper.GetFallbackAccommodations(destination);

        private List<string> GetFallbackActivities(string destination)
            => TravelPlanFallbackHelper.GetFallbackActivities(destination);

        private List<string> GetFallbackTransportation()
            => TravelPlanFallbackHelper.GetFallbackTransportation();

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
