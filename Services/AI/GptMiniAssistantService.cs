using System.Threading.Channels;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

namespace project.Services.AI
{
    /// <summary>
    /// Primary assistant service using gpt-4o-mini (cost-optimized).
    /// </summary>
    public class GptMiniAssistantService : IAiAssistantService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<GptMiniAssistantService> _logger;
        private readonly HttpClient _httpClient;

        public AiAssistantCapabilities Capabilities { get; } = new("gpt-4o-mini", 8000, SupportsStreaming: true, SupportsStructuredJson: true, SupportsToolCalls: false);

        public GptMiniAssistantService(IConfiguration config, ILogger<GptMiniAssistantService> logger, IHttpClientFactory httpFactory)
        {
            _config = config;
            _logger = logger;
            _httpClient = httpFactory.CreateClient();
        }

        public async Task<AiAssistantResponse> SendMessageAsync(AiAssistantRequest request)
        {
            var apiKey = _config["OPENAI_API_KEY"] ?? _config["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("OpenAI API key not configured. Returning stub response.");
                return new AiAssistantResponse("API key not configured.", null, "Configuration error", UsedFallbackModel: false, ModelUsed: Capabilities.ModelName);
            }

            try
            {
                var messages = BuildMessages(request);
                var payload = new
                {
                    model = "gpt-4o-mini",
                    messages = messages,
                    temperature = 0.7,
                    max_tokens = 2000
                };

                var httpReq = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                httpReq.Headers.Add("Authorization", $"Bearer {apiKey}");
                httpReq.Content = JsonContent.Create(payload);

                var response = await _httpClient.SendAsync(httpReq);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
                var text = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "";

                // Try to parse plan delta if present
                PlanDelta? delta = TryParseDelta(text);

                return new AiAssistantResponse(text, delta, "Response from gpt-4o-mini", UsedFallbackModel: false, ModelUsed: Capabilities.ModelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call OpenAI API");
                return new AiAssistantResponse($"Error: {ex.Message}", null, "API call failed", UsedFallbackModel: false, ModelUsed: Capabilities.ModelName);
            }
        }

        public ChannelReader<string> StreamResponseAsync(AiAssistantRequest request)
        {
            var channel = Channel.CreateUnbounded<string>();
            _ = Task.Run(async () =>
            {
                var apiKey = _config["OPENAI_API_KEY"] ?? _config["OpenAI:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    await channel.Writer.WriteAsync("API key not configured.");
                    channel.Writer.Complete();
                    return;
                }

                try
                {
                    var messages = BuildMessages(request);
                    var payload = new
                    {
                        model = "gpt-4o-mini",
                        messages = messages,
                        temperature = 0.7,
                        max_tokens = 2000,
                        stream = true
                    };

                    var httpReq = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                    httpReq.Headers.Add("Authorization", $"Bearer {apiKey}");
                    httpReq.Content = JsonContent.Create(payload);

                    var response = await _httpClient.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var reader = new System.IO.StreamReader(stream);

                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (line.StartsWith("data: "))
                        {
                            var data = line.Substring(6);
                            if (data == "[DONE]") break;

                            try
                            {
                                var chunk = JsonSerializer.Deserialize<StreamChunk>(data);
                                var content = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
                                if (!string.IsNullOrEmpty(content))
                                {
                                    await channel.Writer.WriteAsync(content);
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Streaming failed");
                    await channel.Writer.WriteAsync($"\n[Error: {ex.Message}]");
                }
                finally
                {
                    channel.Writer.Complete();
                }
            });
            return channel.Reader;
        }

        private List<object> BuildMessages(AiAssistantRequest request)
        {
            var messages = new List<object>
            {
                new { role = "system", content = BuildSystemPrompt(request) }
            };

            // Add history
            foreach (var msg in request.History)
            {
                messages.Add(new { role = msg.Role.ToString().ToLower(), content = msg.Content });
            }

            // Add current user prompt
            messages.Add(new { role = "user", content = request.Prompt });

            return messages;
        }

        private string BuildSystemPrompt(AiAssistantRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a helpful travel planning assistant. You can answer questions and modify travel itineraries.");
            sb.AppendLine($"Current trip: {request.Destination}, {request.Days} days, {request.Travelers} travelers, budget: ${request.Budget}");

            if (!string.IsNullOrWhiteSpace(request.CompressedPlanJson))
            {
                sb.AppendLine("\nCurrent itinerary (JSON):");
                sb.AppendLine(request.CompressedPlanJson);
            }

            sb.AppendLine("\n=== CRITICAL INSTRUCTIONS ===");
            sb.AppendLine("When user requests changes, you MUST:");
            sb.AppendLine("1. Explain the changes in natural language");
            sb.AppendLine("2. Include EXACTLY this JSON structure (don't add any extra text to itinerary!):");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"changes\": [");
            sb.AppendLine("    {\"day\": 2, \"addMorning\": [\"Visit Tokyo National Museum\"], \"addAfternoon\": [\"Explore Ueno Park\"]}");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"truncateToDays\": null");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine("\nFor truncation (e.g., \"change to 3 days\"):");
            sb.AppendLine("- Set truncateToDays to the requested number");
            sb.AppendLine("- Use \"changes\" array ONLY if you need to modify specific activities");
            sb.AppendLine("- Do NOT add Polish text like 'Zwiedzanie X' - use proper activity names");
            sb.AppendLine("\nIMPORTANT: Activities must be in English and match existing format (e.g., '📍 Museum Name' or 'Visit X').");

            return sb.ToString();
        }

        private PlanDelta? TryParseDelta(string text)
        {
            try
            {
                // Look for JSON block in markdown
                var start = text.IndexOf("```json");
                if (start < 0) start = text.IndexOf("{\"changes\":");
                if (start < 0) return null;

                var end = text.IndexOf("```", start + 7);
                if (end < 0) end = text.Length;

                var json = text.Substring(start, end - start)
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                var parsed = JsonSerializer.Deserialize<PlanDeltaDto>(json);
                if (parsed?.Changes != null || parsed?.TruncateToDays != null)
                {
                    var changes = (parsed.Changes ?? new List<DayChangeDto>()).Select(c => new DayChange(
                        c.Day,
                        c.AddMorning?.ToList(),
                        c.AddAfternoon?.ToList(),
                        c.AddEvening?.ToList(),
                        c.Remove?.ToList(),
                        c.Note
                    )).ToList();
                    return new PlanDelta(changes, parsed.TruncateToDays);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse plan delta from response");
            }
            return null;
        }

        private class OpenAIResponse
        {
            [JsonPropertyName("choices")]
            public List<Choice>? Choices { get; set; }
        }

        private class Choice
        {
            [JsonPropertyName("message")]
            public Message? Message { get; set; }
        }

        private class Message
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }

        private class StreamChunk
        {
            [JsonPropertyName("choices")]
            public List<StreamChoice>? Choices { get; set; }
        }

        private class StreamChoice
        {
            [JsonPropertyName("delta")]
            public Delta? Delta { get; set; }
        }

        private class Delta
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }

        private class PlanDeltaDto
        {
            [JsonPropertyName("changes")]
            public List<DayChangeDto>? Changes { get; set; }
            [JsonPropertyName("truncateToDays")]
            public int? TruncateToDays { get; set; }
        }

        private class DayChangeDto
        {
            [JsonPropertyName("day")]
            public int Day { get; set; }
            [JsonPropertyName("addMorning")]
            public List<string>? AddMorning { get; set; }
            [JsonPropertyName("addAfternoon")]
            public List<string>? AddAfternoon { get; set; }
            [JsonPropertyName("addEvening")]
            public List<string>? AddEvening { get; set; }
            [JsonPropertyName("remove")]
            public List<string>? Remove { get; set; }
            [JsonPropertyName("note")]
            public string? Note { get; set; }
        }
    }
}
