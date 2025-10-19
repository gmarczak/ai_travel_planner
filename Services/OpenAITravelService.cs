using OpenAI;
using OpenAI.Chat;
using project.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace project.Services
{
    public class OpenAITravelService : ITravelService
    {
        private readonly OpenAIClient _openAIClient;
        private readonly ILogger<OpenAITravelService> _logger;
        private readonly IConfiguration _configuration;

        public OpenAITravelService(ILogger<OpenAITravelService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is not configured. Please add 'OpenAI:ApiKey' to your configuration.");
            }

            _openAIClient = new OpenAIClient(apiKey);
        }

        public async Task<TravelPlan> GenerateTravelPlanAsync(TravelPlanRequest request)
        {
            try
            {
                _logger.LogInformation("Generating AI travel plan for {Destination}", request.Destination);

                // CREATE PROMPT FOR OPENAI
                var prompt = CreateTravelPlanPrompt(request);

                var chatClient = _openAIClient.GetChatClient("gpt-3.5-turbo");

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are an expert travel planner. Create detailed, personalized travel itineraries based on user preferences. Always respond in JSON format."),
                    new UserChatMessage(prompt)
                };

                var completion = await chatClient.CompleteChatAsync(messages);

                var responseContent = completion.Value.Content[0].Text;
                _logger.LogInformation("Received response from OpenAI: {Response}", responseContent);

                // PARSE JSON RESPONSE
                TravelPlanResponse? aiResponse = null;
                try
                {
                    _logger.LogInformation("Attempting to deserialize JSON response");

                    // CLEAN JSON: extract first complete JSON object (handle extra closing braces)
                    var cleanedJson = CleanJsonResponse(responseContent);

                    aiResponse = JsonSerializer.Deserialize<TravelPlanResponse>(cleanedJson);

                    if (aiResponse != null)
                    {
                        _logger.LogInformation("JSON deserialized successfully. Itinerary length: {Length}, Accommodations: {AccommodationCount}, Activities: {ActivityCount}",
                            aiResponse.Itinerary?.Length ?? 0,
                            aiResponse.Accommodations?.Count ?? 0,
                            aiResponse.Activities?.Count ?? 0);
                    }
                    else
                    {
                        _logger.LogWarning("JSON deserialized to null");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("Failed to parse AI response as JSON: {Error}. Raw response: {Response}", ex.Message, responseContent);
                    // FALLBACK: CREATE PLAN FROM TEXT
                    return CreateFallbackPlan(request, responseContent);
                }

                // BUILD TRAVELPLAN FROM AI RESPONSE
                return new TravelPlan
                {
                    Id = new Random().Next(1, 10000),
                    Destination = request.Destination,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    NumberOfTravelers = request.NumberOfTravelers,
                    Budget = request.Budget,
                    TravelPreferences = request.TravelPreferences ?? "",
                    CreatedAt = DateTime.Now,
                    GeneratedItinerary = aiResponse?.Itinerary ?? responseContent,
                    Accommodations = aiResponse?.Accommodations ?? GetFallbackAccommodations(request.Destination),
                    Activities = aiResponse?.Activities ?? GetFallbackActivities(request.Destination),
                    Transportation = aiResponse?.Transportation ?? GetFallbackTransportation()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API for destination {Destination}", request.Destination);

                // FALLBACK TO MOCK DATA ON FAILURE
                _logger.LogInformation("Falling back to mock travel service");
                return await CreateFallbackPlan(request);
            }
        }

        public async Task<List<TravelSuggestion>> GetDestinationSuggestionsAsync(string query)
        {
            try
            {
                var chatClient = _openAIClient.GetChatClient("gpt-3.5-turbo");

                var prompt = $"Suggest 5 travel destinations that match the query '{query}'. " +
                           "Respond with a JSON array of objects with 'name' and 'description' properties. " +
                           "Keep descriptions short (under 50 characters).";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are a travel destination expert. Always respond with valid JSON."),
                    new UserChatMessage(prompt)
                };

                var completion = await chatClient.CompleteChatAsync(messages);

                var responseContent = completion.Value.Content[0].Text;

                try
                {
                    var suggestions = JsonSerializer.Deserialize<List<DestinationSuggestion>>(responseContent);
                    return suggestions?.Select(s => new TravelSuggestion
                    {
                        Type = "Destination",
                        Name = s.Name,
                        Description = s.Description,
                        Priority = 1
                    }).ToList() ?? new List<TravelSuggestion>();
                }
                catch (JsonException)
                {
                    // FALLBACK TO SIMPLE SUGGESTIONS
                    return GetFallbackDestinationSuggestions(query);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting AI destination suggestions for query: {Query}", query);
                return GetFallbackDestinationSuggestions(query);
            }
        }

        private string CreateTravelPlanPrompt(TravelPlanRequest request)
        {
            var days = (request.EndDate - request.StartDate).Days + 1;
            // DEFAULT INTERESTS: GENERAL SIGHTSEEING
            var interestsText = "general sightseeing";
            var tripType = !string.IsNullOrWhiteSpace(request.TripType) ? request.TripType : "General";
            var preferences = !string.IsNullOrWhiteSpace(request.TravelPreferences) ? request.TravelPreferences : "None specified";

            return $@"Create a detailed {days}-day travel plan for {request.Destination} for {request.NumberOfTravelers} travelers with a budget of ${request.Budget}.

Trip details:
- Dates: {request.StartDate:MMM dd, yyyy} to {request.EndDate:MMM dd, yyyy}
- Travelers: {request.NumberOfTravelers}
- Budget: ${request.Budget} (approximately ${request.Budget / request.NumberOfTravelers / days:F0} per person per day)
- Interests: {interestsText}
- Trip type: {tripType}
- Additional preferences: {preferences}

IMPORTANT: You MUST create a plan for ALL {days} DAYS. Do not skip any days!

For each day, include:
- Specific attractions, museums, or landmarks to visit (with actual names)
- Restaurant recommendations for lunch and dinner (with names and cuisine types)
- Activities or experiences unique to that location
- Transportation tips between major locations
- Practical tips or insider advice
- Estimated costs for major activities when relevant

Format each day clearly as:
Day 1 ({request.StartDate:MMM dd, yyyy}): Brief overview of the day
- Morning: Visit [specific place/attraction name], description of what to see
- Lunch: [Restaurant name], [cuisine type], [price range]
- Afternoon: Explore [specific location], what to do there
- Dinner: [Restaurant name], [cuisine type], atmosphere
- Evening: Optional activity or recommendation
- Tips: Practical advice for this day

Day 2 ({request.StartDate.AddDays(1):MMM dd, yyyy}): Overview
...continue for ALL {days} days...

Please respond with a JSON object in this exact format:
{{{{
    ""itinerary"": ""Complete day-by-day itinerary for ALL {days} DAYS as a single string with line breaks"",
    ""accommodations"": [""Hotel name with location and brief description"", ""Alternative hotel option"", ""Budget-friendly option""],
    ""activities"": [""Must-see attraction 1 with description"", ""Activity 2"", ""Activity 3"", ""Activity 4"", ""Hidden gem or local favorite""],
    ""transportation"": [""Primary transport option with details"", ""Alternative transport method"", ""Tips for getting around efficiently""]
}}}}

CRITICAL: Include ALL {days} days in the itinerary. Make it detailed and specific with real place names, restaurants, and practical recommendations.";
        }

        private TravelPlan CreateFallbackPlan(TravelPlanRequest request, string aiResponse = "")
        {
            var days = (request.EndDate - request.StartDate).Days + 1;

            var itinerary = string.IsNullOrWhiteSpace(aiResponse)
                    ? $"?? AI-Generated {days}-Day Plan for {request.Destination}\n\n" +
                        "Sorry, we're experiencing technical difficulties with our AI service.\n" +
                        "Please try again later for a fully personalized itinerary."
                    : aiResponse;

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
                GeneratedItinerary = itinerary,
                Accommodations = GetFallbackAccommodations(request.Destination),
                Activities = GetFallbackActivities(request.Destination),
                Transportation = GetFallbackTransportation()
            };
        }

        private async Task<TravelPlan> CreateFallbackPlan(TravelPlanRequest request)
        {
            await Task.Delay(500); // SIMULATE PROCESSING TIME
            return CreateFallbackPlan(request, "");
        }

        private List<string> GetFallbackAccommodations(string destination)
        {
            return new List<string>
            {
                $"Recommended Hotel in {destination}",
                $"Boutique Accommodation near {destination}",
                $"Budget-Friendly Option in {destination}",
                $"Luxury Resort in {destination}"
            };
        }

        private List<string> GetFallbackActivities(string destination)
        {
            return new List<string>
            {
                $"City Tour of {destination}",
                $"Local Food Experience in {destination}",
                $"Historic Sites in {destination}",
                $"Shopping District in {destination}"
            };
        }

        private List<string> GetFallbackTransportation()
        {
            return new List<string>
            {
                "Airport Transfer Service",
                "Public Transportation Pass",
                "Car Rental Options",
                "Taxi and Ride-sharing Services"
            };
        }

        private List<TravelSuggestion> GetFallbackDestinationSuggestions(string query)
        {
            var destinations = new List<string>
            {
                "Paris, France", "Tokyo, Japan", "New York, USA", "Rome, Italy",
                "Barcelona, Spain", "London, UK", "Amsterdam, Netherlands",
                "Prague, Czech Republic", "Istanbul, Turkey", "Bangkok, Thailand"
            };

            return destinations
                .Where(d => d.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .Select(d => new TravelSuggestion
                {
                    Type = "Destination",
                    Name = d,
                    Description = "Popular travel destination",
                    Priority = 1
                })
                .ToList();
        }

        /// <summary>
        /// Cleans AI JSON response by extracting the first complete JSON object,
        /// handling cases where extra closing braces or whitespace appear after valid JSON.
        /// </summary>
        private string CleanJsonResponse(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
                return rawJson;

            // Trim whitespace
            var trimmed = rawJson.Trim();

            // Find first { and track brace depth to extract complete object
            int start = trimmed.IndexOf('{');
            if (start == -1)
                return trimmed;

            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < trimmed.Length; i++)
            {
                char c = trimmed[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        // Found complete JSON object
                        return trimmed.Substring(start, i - start + 1);
                    }
                }
            }

            // If we didn't find matching braces, return original
            return trimmed;
        }
    }

    // JSON DESERIALIZATION HELPERS
    public class TravelPlanResponse
    {
        [JsonPropertyName("itinerary")]
        public string Itinerary { get; set; } = string.Empty;

        [JsonPropertyName("accommodations")]
        public List<string> Accommodations { get; set; } = new();

        [JsonPropertyName("activities")]
        public List<string> Activities { get; set; } = new();

        [JsonPropertyName("transportation")]
        public List<string> Transportation { get; set; } = new();
    }

    public class DestinationSuggestion
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }
}