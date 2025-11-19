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
    /// Orchestrates selection between gpt-4o-mini and gpt-4.1-mini (second is still stub).
    /// </summary>
    public class FallbackAssistantService : IAiAssistantService
    {
        private readonly GptMiniAssistantService _mini;
        private readonly Gpt41MiniAssistantService _fortyOne; // stub secondary

        public FallbackAssistantService(GptMiniAssistantService mini, Gpt41MiniAssistantService fortyOne)
        {
            _mini = mini;
            _fortyOne = fortyOne;
        }

        public AiAssistantCapabilities Capabilities => _mini.Capabilities;

        public async Task<AiAssistantResponse> SendMessageAsync(AiAssistantRequest request)
        {
            bool escalate = request.ForceHigherQuality || AssistantComplexityEvaluator.IsComplex(request.Prompt, mentionedDaysCount: CountDayMentions(request.Prompt));
            if (!escalate)
            {
                var baseResp = await _mini.SendMessageAsync(request);
                return baseResp with { UsedFallbackModel = false };
            }
            var higher = await _fortyOne.SendMessageAsync(request);
            return higher with { UsedFallbackModel = true };
        }

        public ChannelReader<string> StreamResponseAsync(AiAssistantRequest request)
        {
            bool escalate = request.ForceHigherQuality || AssistantComplexityEvaluator.IsComplex(request.Prompt, mentionedDaysCount: CountDayMentions(request.Prompt));
            return escalate ? _fortyOne.StreamResponseAsync(request) : _mini.StreamResponseAsync(request);
        }

        private static int CountDayMentions(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return 0;
            int count = 0;
            for (int d = 1; d <= 31; d++)
            {
                if (prompt.Contains($"day {d}", System.StringComparison.OrdinalIgnoreCase) || prompt.Contains($"dzień {d}", System.StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Fallback service using gpt-4o-mini (OpenAI doesn't have 4.1-mini, using 4o-mini with higher quality settings).
    /// </summary>
    public class Gpt41MiniAssistantService : IAiAssistantService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<Gpt41MiniAssistantService> _logger;
        private readonly HttpClient _httpClient;

        public AiAssistantCapabilities Capabilities { get; } = new("gpt-4o-mini-high", 8000, SupportsStreaming: true, SupportsStructuredJson: true, SupportsToolCalls: false);

        public Gpt41MiniAssistantService(IConfiguration config, ILogger<Gpt41MiniAssistantService> logger, IHttpClientFactory httpFactory)
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
                _logger.LogWarning("OpenAI API key not configured for fallback.");
                return new AiAssistantResponse("API key not configured.", null, "Configuration error", UsedFallbackModel: true, ModelUsed: Capabilities.ModelName);
            }

            try
            {
                var messages = BuildMessages(request);
                var payload = new
                {
                    model = "gpt-4o-mini",
                    messages = messages,
                    temperature = 0.5, // Lower temperature for more precise complex tasks
                    max_tokens = 3000  // More tokens for complex responses
                };

                var httpReq = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                httpReq.Headers.Add("Authorization", $"Bearer {apiKey}");
                httpReq.Content = JsonContent.Create(payload);

                var response = await _httpClient.SendAsync(httpReq);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
                var text = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "";

                PlanDelta? delta = TryParseDelta(text);

                return new AiAssistantResponse(text, delta, "Complex task - using higher quality settings", UsedFallbackModel: true, ModelUsed: Capabilities.ModelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call OpenAI API (fallback)");
                return new AiAssistantResponse($"Error: {ex.Message}", null, "API call failed", UsedFallbackModel: true, ModelUsed: Capabilities.ModelName);
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
                        temperature = 0.5,
                        max_tokens = 3000,
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
                    _logger.LogError(ex, "Streaming failed (fallback)");
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

            foreach (var msg in request.History)
            {
                messages.Add(new { role = msg.Role.ToString().ToLower(), content = msg.Content });
            }

            messages.Add(new { role = "user", content = request.Prompt });

            return messages;
        }

        private string BuildSystemPrompt(AiAssistantRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert travel planning assistant handling complex itinerary modifications.");
            sb.AppendLine("Pay special attention to details, constraints, and user preferences.");
            sb.AppendLine($"Current trip: {request.Destination}, {request.Days} days, {request.Travelers} travelers, budget: ${request.Budget}");

            if (!string.IsNullOrWhiteSpace(request.CompressedPlanJson))
            {
                sb.AppendLine("\nCurrent itinerary (JSON):");
                sb.AppendLine(request.CompressedPlanJson);
            }

            sb.AppendLine("\n=== MODIFICATION RULES ===");
            sb.AppendLine("Provide detailed explanation, then include JSON:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"changes\": [{\"day\": 1, \"addMorning\": [\"Visit Museum\"], \"note\": \"Added cultural activity\"}],");
            sb.AppendLine("  \"truncateToDays\": null");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine("For truncation: set truncateToDays to requested days, don't add random Polish text.");
            sb.AppendLine("Activities MUST be in English and match itinerary format (e.g., '📍 Location' or 'Visit X').");

            return sb.ToString();
        }

        private PlanDelta? TryParseDelta(string text)
        {
            try
            {
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
                _logger.LogWarning(ex, "Failed to parse plan delta from fallback response");
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
