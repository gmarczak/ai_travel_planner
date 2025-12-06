using OpenAI;
using OpenAI.Chat;
using System.Text.Json;
using System.Text.Json.Serialization;
using project.Models;

namespace project.Services
{
    public class OpenAITravelService : ITravelService, IAiService
    {
        private readonly OpenAIClient _openAIClient;
        private readonly ILogger<OpenAITravelService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IAiCacheService _cacheService;
        private readonly JsonSerializerOptions _jsonOptions;

        public string ProviderName => "OpenAI";

        public OpenAITravelService(ILogger<OpenAITravelService> logger, IConfiguration configuration, IAiCacheService cacheService)
        {
            _logger = logger;
            _configuration = configuration;
            _cacheService = cacheService;

            // Configure JSON options for better parsing
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is not configured. Please add 'OpenAI:ApiKey' to your configuration.");
            }

            _openAIClient = new OpenAIClient(apiKey);
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                // Simple health check: try to get models or make a minimal request
                var chatClient = _openAIClient.GetChatClient("gpt-3.5-turbo");
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("Respond with 'OK'"),
                    new UserChatMessage("Test")
                };
                var completion = await chatClient.CompleteChatAsync(messages);
                return !string.IsNullOrEmpty(completion.Value.Content[0].Text);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenAI service is not available");
                return false;
            }
        }

        // Multi-step AI: Etap 1 - generowanie listy miejsc
        public async Task<PlaceListResponse?> GeneratePlaceListAsync(TravelPlanRequest request)
        {
            _logger.LogInformation("Generating place list for {Destination}", request.Destination);
            var prompt = CreatePlaceListPrompt(request);

            // Sprawdź cache
            var cachedResponse = await _cacheService.GetCachedResponseAsync(prompt, "gpt-3.5-turbo-places");
            if (cachedResponse != null)
            {
                try
                {
                    var cleanedJson = CleanJsonResponse(cachedResponse);
                    var cachedResult = JsonSerializer.Deserialize<PlaceListResponse>(cleanedJson, _jsonOptions);
                    if (cachedResult != null)
                    {
                        _logger.LogInformation("Returning cached place list for {Destination}", request.Destination);
                        return cachedResult;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize cached place list, fetching new one");
                }
            }

            var chatClient = _openAIClient.GetChatClient("gpt-3.5-turbo");
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are a JSON-only API. You must respond with valid JSON only. No markdown, no code blocks, no additional text. Always use double quotes for JSON strings."),
                new UserChatMessage(prompt)
            };
            var completion = await chatClient.CompleteChatAsync(messages);
            var responseContent = completion.Value.Content[0].Text;
            _logger.LogInformation("Received place list response: {Response}", responseContent);

            // Cache response (7 dni expiry)
            await _cacheService.CacheResponseAsync(prompt, responseContent, "gpt-3.5-turbo-places", 0, TimeSpan.FromDays(7));

            try
            {
                var cleanedJson = CleanJsonResponse(responseContent);
                var result = JsonSerializer.Deserialize<PlaceListResponse>(cleanedJson, _jsonOptions);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse place list JSON");
                return null;
            }
        }

        private string CreatePlaceListPrompt(TravelPlanRequest request)
        {
            var days = (request.EndDate - request.StartDate).Days + 1;
            var tripType = !string.IsNullOrWhiteSpace(request.TripType) ? request.TripType : "General";
            var preferences = !string.IsNullOrWhiteSpace(request.TravelPreferences) ? request.TravelPreferences : "None specified";

            var transportContext = "";
            if (!string.IsNullOrWhiteSpace(request.TransportMode))
            {
                var departureInfo = !string.IsNullOrWhiteSpace(request.DepartureLocation)
                    ? $" from {request.DepartureLocation}"
                    : "";

                transportContext = request.TransportMode switch
                {
                    "Flight" => $"\n\n✈️ TRANSPORT MODE: Flying{departureInfo}\n- Suggest nearby alternative airports for both departure and arrival (budget airlines, better connections)\n- Mention flight duration and typical costs{departureInfo}\n- Airport transfer options (bus, train, taxi estimates)\n- Include specific airport codes",
                    "Car" => "\n\n🚗 TRANSPORT MODE: Driving\n- Mention scenic routes and road trip highlights\n- Include parking information for attractions\n- If water crossing needed, mention ferry routes with booking tips",
                    "Train" => "\n\n🚆 TRANSPORT MODE: Train\n- Mention main train stations and connections\n- Include information about regional rail passes\n- Suggest train-accessible destinations and day trips",
                    "Bus" => "\n\n🚌 TRANSPORT MODE: Bus\n- Mention main bus terminals and international routes (FlixBus, etc.)\n- Include information about city bus passes\n- Suggest bus-accessible destinations",
                    _ => ""
                };
            }

            return $@"You must respond with ONLY valid JSON. No additional text, no markdown formatting, no code blocks.

Task: Suggest the top {days * 4} must-see places for a {days}-day trip to {request.Destination}.
- Travelers: {request.NumberOfTravelers}
- Trip type: {tripType}

⚠️ CRITICAL: User preferences that MUST influence place selection:
{preferences}{transportContext}

Select places that match these preferences.

Required JSON format:
{{
  ""places"": [
    {{
      ""name"": ""Place name"",
      ""type"": ""attraction/restaurant/museum/etc"",
      ""location"": ""Specific area or address"",
      ""description"": ""Brief description (1-2 sentences)""
    }}
  ]
}}

Return ONLY the JSON object above. No extra text before or after.";
        }

        // Multi-step AI: Etap 2 - generowanie szczegółów dla miejsca
        public async Task<string?> GeneratePlaceDetailsAsync(PlaceInfo place, TravelPlanRequest request)
        {
            _logger.LogInformation("Generating details for place: {PlaceName}", place.Name);
            var prompt = CreatePlaceDetailsPrompt(place, request);

            // Sprawdź cache
            var cachedResponse = await _cacheService.GetCachedResponseAsync(prompt, "gpt-3.5-turbo-details");
            if (cachedResponse != null)
            {
                _logger.LogInformation("Returning cached details for {PlaceName}", place.Name);
                return cachedResponse;
            }

            var chatClient = _openAIClient.GetChatClient("gpt-3.5-turbo");
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are an expert travel guide. Provide detailed information about specific places. Be concise but informative."),
                new UserChatMessage(prompt)
            };

            try
            {
                var completion = await chatClient.CompleteChatAsync(messages);
                var responseContent = completion.Value.Content[0].Text;
                _logger.LogInformation("Received place details for {PlaceName}", place.Name);

                // Cache response (30 dni expiry)
                await _cacheService.CacheResponseAsync(prompt, responseContent, "gpt-3.5-turbo-details", 0, TimeSpan.FromDays(30));

                return responseContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate details for place: {PlaceName}", place.Name);
                return $"{place.Name}: {place.Description ?? "A must-see attraction in the area."}";
            }
        }

        private string CreatePlaceDetailsPrompt(PlaceInfo place, TravelPlanRequest request)
        {
            return $@"Provide a brief, structured description for '{place.Name}' in {request.Destination}.
Format your response as a single paragraph with 1-2 short sentences.
Focus on: what makes it special, best time to visit, and one practical tip.
Keep it under 100 words total.";
        }

        public async Task<TravelPlan> GenerateTravelPlanAsync(TravelPlanRequest request)
        {
            try
            {
                _logger.LogInformation("Generating AI travel plan for {Destination} using MULTI-STEP approach", request.Destination);

                // MULTI-STEP: Etap 1 - Generuj listę miejsc
                var placeList = await GeneratePlaceListAsync(request);

                string itineraryText;
                List<string> activities = new();

                if (placeList != null && placeList.Places.Any())
                {
                    _logger.LogInformation("Generated {Count} places, now fetching details...", placeList.Places.Count);

                    // MULTI-STEP: Etap 2 - Generuj szczegóły dla każdego miejsca (parallel dla szybkości)
                    var detailsTasks = placeList.Places.Take(10).Select(place => GeneratePlaceDetailsAsync(place, request));
                    var details = await Task.WhenAll(detailsTasks);

                    // Buduj itinerary z miejsc i ich szczegółów
                    var days = (request.EndDate - request.StartDate).Days + 1;
                    var placesPerDay = Math.Max(1, placeList.Places.Count / days);

                    var itineraryBuilder = new System.Text.StringBuilder();
                    itineraryBuilder.AppendLine($"🗺️ {days}-Day Travel Plan for {request.Destination}");
                    itineraryBuilder.AppendLine($"📅 {request.StartDate:MMM dd} - {request.EndDate:MMM dd, yyyy}");
                    itineraryBuilder.AppendLine();

                    int placeIndex = 0;
                    for (int day = 1; day <= days && placeIndex < placeList.Places.Count; day++)
                    {
                        var dayDate = request.StartDate.AddDays(day - 1);
                        itineraryBuilder.AppendLine($"Day {day} - {dayDate:dddd, MMMM dd, yyyy}");
                        itineraryBuilder.AppendLine();

                        for (int i = 0; i < placesPerDay && placeIndex < placeList.Places.Count; i++, placeIndex++)
                        {
                            var place = placeList.Places[placeIndex];
                            var detail = placeIndex < details.Length ? details[placeIndex] : place.Description;

                            // Format place with type/category
                            var timeOfDay = i == 0 ? "🌅 Morning" : (i == 1 ? "☀️ Afternoon" : "🌆 Evening");
                            itineraryBuilder.AppendLine($"{timeOfDay}:");
                            itineraryBuilder.AppendLine($"📍 {place.Name}");

                            if (!string.IsNullOrWhiteSpace(place.Location))
                            {
                                itineraryBuilder.AppendLine($"   📌 Location: {place.Location}");
                            }

                            if (!string.IsNullOrWhiteSpace(detail))
                            {
                                // Split long descriptions into multiple lines for readability
                                var sentences = detail.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                                foreach (var sentence in sentences)
                                {
                                    if (!string.IsNullOrWhiteSpace(sentence))
                                    {
                                        itineraryBuilder.AppendLine($"   ℹ️  {sentence.Trim()}.");
                                    }
                                }
                            }
                            itineraryBuilder.AppendLine();

                            activities.Add(place.Name);
                        }
                    }

                    itineraryText = itineraryBuilder.ToString();
                }
                else
                {
                    _logger.LogWarning("Failed to generate place list, falling back to single-step generation");

                    // FALLBACK: użyj starej metody (single-step)
                    var prompt = CreateTravelPlanPrompt(request);
                    var chatClient = _openAIClient.GetChatClient("gpt-3.5-turbo");
                    var messages = new List<ChatMessage>
                    {
                        new SystemChatMessage("You are a JSON-only API. You must respond with valid JSON only. No markdown, no code blocks, no additional text. Always use double quotes for JSON strings."),
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
                        var cleanedJson = CleanJsonResponse(responseContent);
                        aiResponse = JsonSerializer.Deserialize<TravelPlanResponse>(cleanedJson, _jsonOptions);

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
                        return CreateFallbackPlan(request, responseContent);
                    }

                    itineraryText = aiResponse?.Itinerary ?? responseContent;
                    activities = aiResponse?.Activities ?? GetFallbackActivities(request.Destination);
                }

                // BUILD TRAVELPLAN (works for both multi-step and fallback paths)
                return new TravelPlan
                {
                    Id = new Random().Next(1, 10000),
                    Destination = request.Destination,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    NumberOfTravelers = request.NumberOfTravelers,
                    Budget = request.Budget ?? 0m,
                    TravelPreferences = request.TravelPreferences ?? "",
                    TransportMode = request.TransportMode,
                    DepartureLocation = request.DepartureLocation,
                    CreatedAt = DateTime.Now,
                    GeneratedItinerary = itineraryText,
                    Accommodations = GetFallbackAccommodations(request.Destination),
                    Activities = activities,
                    Transportation = GetFallbackTransportation()
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

                var prompt = $@"You must respond with ONLY valid JSON. No additional text, no markdown, no code blocks.

Task: Suggest 5 travel destinations matching the query: '{query}'

Required JSON format:
[
  {{
    ""name"": ""Destination name"",
    ""description"": ""Short description (max 50 chars)""
  }}
]

Return ONLY the JSON array above.";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are a JSON-only API. Respond with valid JSON only. No markdown, no code blocks, no additional text."),
                    new UserChatMessage(prompt)
                };

                var completion = await chatClient.CompleteChatAsync(messages);

                var responseContent = completion.Value.Content[0].Text;

                try
                {
                    var cleanedJson = CleanJsonResponse(responseContent);
                    var suggestions = JsonSerializer.Deserialize<List<DestinationSuggestion>>(cleanedJson, _jsonOptions);
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
            var interestsText = "general sightseeing";
            var tripType = !string.IsNullOrWhiteSpace(request.TripType) ? request.TripType : "General";
            var preferences = !string.IsNullOrWhiteSpace(request.TravelPreferences) ? request.TravelPreferences : "None specified";
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
                    "Flight" => $"\n\n✈️ TRANSPORT MODE: Flying{departureInfo}\nIn the 'transportation' array, include:\n- Specific flight routes: {request.DepartureLocation ?? "major hubs"} → {request.Destination} (with nearby alternative airports for BOTH cities)\n- Example: Barcelona has El Prat-BCN (main), Girona-GRO (Ryanair), Reus-REU (budget)\n- Estimated flight duration and typical costs for this specific route\n- Airport transfer options at destination (train, bus, taxi) with prices\n- Best booking websites and timing tips{departureDetail}",
                    "Car" => "\n\n🚗 TRANSPORT MODE: Driving\nIn the 'transportation' array, include:\n- Scenic driving routes and road trip highlights\n- Parking information and costs at major attractions\n- Car rental tips and estimated costs\n- Ferry routes if water crossing needed (e.g., Portsmouth-Santander for Spain) with booking websites",
                    "Train" => "\n\n🚆 TRANSPORT MODE: Train\nIn the 'transportation' array, include:\n- Main train stations and connections\n- Regional/national rail passes (Eurail, etc.) with prices\n- Recommended day trips accessible by train\n- Booking websites (Trainline, local railways)",
                    "Bus" => "\n\n🚌 TRANSPORT MODE: Bus\nIn the 'transportation' array, include:\n- Main bus terminals and international routes\n- FlixBus, Eurolines, or regional bus companies\n- Long-distance bus passes and prices\n- Booking websites and tips",
                    _ => ""
                };
            }

            return $@"You must respond with ONLY valid JSON. No additional text, no markdown formatting, no code blocks.

Task: Create a detailed {days}-day travel plan for {request.Destination}.
- Dates: {request.StartDate:MMM dd, yyyy} to {request.EndDate:MMM dd, yyyy}
- Travelers: {request.NumberOfTravelers}
- Budget: ${budgetValue} (approximately {(budgetValue > 0 ? (budgetValue / request.NumberOfTravelers / days).ToString("F0") : "0")} per person per day)
- Interests: {interestsText}
- Trip type: {tripType}

⚠️ IMPORTANT USER PREFERENCES (must be incorporated throughout the itinerary):
{preferences}{transportInstructions}

Ensure activities, restaurants, and experiences align with the above preferences in EVERY day of the itinerary.

Required JSON format:
{{
  ""itinerary"": ""Day 1 ({request.StartDate:MMM dd, yyyy}): Overview\n- Morning: ...\n- Lunch: ...\nDay 2: ...[continue for ALL {days} days]"",
  ""accommodations"": [""Hotel 1"", ""Hotel 2"", ""Hotel 3""],
  ""activities"": [""Activity 1"", ""Activity 2"", ""Activity 3""],
  ""transportation"": [""Transport option 1"", ""Transport option 2""]
}}

For the itinerary string, include for EACH day:
- Morning: Specific place/attraction with description
- Lunch: Restaurant name, cuisine type, price range
- Afternoon: Activity or location with details
- Dinner: Restaurant name, cuisine type
- Evening: Optional activity
- Tips: Practical advice

CRITICAL: Include ALL {days} days. Use \\n for line breaks in the itinerary string. Return ONLY the JSON object.";
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
                Budget = request.Budget ?? 0m,
                TravelPreferences = request.TravelPreferences ?? string.Empty,
                TransportMode = request.TransportMode,
                DepartureLocation = request.DepartureLocation,
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
            => TravelPlanFallbackHelper.GetFallbackAccommodations(destination);

        private List<string> GetFallbackActivities(string destination)
            => TravelPlanFallbackHelper.GetFallbackActivities(destination);

        private List<string> GetFallbackTransportation()
            => TravelPlanFallbackHelper.GetFallbackTransportation();

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
        /// Cleans AI JSON response by extracting valid JSON from various formats:
        /// - Markdown code blocks (```json ... ```)
        /// - Plain text with JSON embedded
        /// - Extra whitespace or text before/after JSON
        /// </summary>
        private string CleanJsonResponse(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
                return rawJson;

            var trimmed = rawJson.Trim();

            // Remove markdown code blocks: ```json ... ``` or ``` ... ```
            if (trimmed.StartsWith("```"))
            {
                var lines = trimmed.Split('\n');
                var jsonLines = new List<string>();
                bool inCodeBlock = false;

                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("```"))
                    {
                        inCodeBlock = !inCodeBlock;
                        continue;
                    }
                    if (inCodeBlock)
                    {
                        jsonLines.Add(line);
                    }
                }

                if (jsonLines.Any())
                {
                    trimmed = string.Join("\n", jsonLines).Trim();
                }
            }

            // Find first { or [ and track depth to extract complete JSON
            int start = -1;
            char startChar = '\0';

            for (int i = 0; i < trimmed.Length; i++)
            {
                if (trimmed[i] == '{' || trimmed[i] == '[')
                {
                    start = i;
                    startChar = trimmed[i];
                    break;
                }
            }

            if (start == -1)
                return trimmed;

            char endChar = startChar == '{' ? '}' : ']';
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

                if (c == startChar)
                    depth++;
                else if (c == endChar)
                {
                    depth--;
                    if (depth == 0)
                    {
                        // Found complete JSON object/array
                        return trimmed.Substring(start, i - start + 1);
                    }
                }
            }

            // If we didn't find matching braces, return from start to end
            return trimmed.Substring(start);
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