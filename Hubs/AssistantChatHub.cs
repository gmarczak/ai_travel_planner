using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using project.Services.AI;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System;

namespace project.Hubs
{
    public class HistoryItem
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime? Timestamp { get; set; }
    }

    /// <summary>
    /// SignalR hub for AI assistant chat streaming.
    /// </summary>
    public class AssistantChatHub : Hub
    {
        private readonly IAiAssistantService _assistant;
        private readonly ILogger<AssistantChatHub> _logger;
        private readonly AssistantRateLimiter _rateLimiter;
        private readonly AssistantTelemetryService _telemetry;

        public AssistantChatHub(
            IAiAssistantService assistant,
            ILogger<AssistantChatHub> logger,
            AssistantRateLimiter rateLimiter,
            AssistantTelemetryService telemetry)
        {
            _assistant = assistant;
            _logger = logger;
            _rateLimiter = rateLimiter;
            _telemetry = telemetry;
        }

        public async Task SendMessage(string userId, string prompt, string planJson, string destination, int days, int travelers, decimal budget, string? chatHistoryJson = null)
        {
            try
            {
                // Rate limiting
                var rateCheck = await _rateLimiter.CheckRateLimitAsync(userId);
                if (!rateCheck.Allowed)
                {
                    await Clients.Caller.SendAsync("Error", rateCheck.Message);
                    return;
                }

                // Parse chat history if provided
                var history = new List<ChatMessage>();
                if (!string.IsNullOrWhiteSpace(chatHistoryJson))
                {
                    try
                    {
                        var historyItems = JsonSerializer.Deserialize<List<HistoryItem>>(chatHistoryJson);
                        if (historyItems != null && historyItems.Count > 0)
                        {
                            // Take last 5 messages for context
                            var recent = historyItems.TakeLast(5).ToList();
                            foreach (var item in recent)
                            {
                                var role = item.Role == "user" ? ChatRole.User : ChatRole.Assistant;
                                history.Add(new ChatMessage(role, item.Content, null, item.Timestamp));
                            }
                            _logger.LogInformation("[Assistant] Using {Count} messages from history as context", history.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Assistant] Failed to parse chat history");
                    }
                }

                // Build request
                var request = new AiAssistantRequest(
                    UserId: userId,
                    Prompt: prompt,
                    Destination: destination,
                    Days: days,
                    Travelers: travelers,
                    Budget: budget,
                    History: history,
                    CompressedPlanJson: CompressPlan(planJson),
                    ForceHigherQuality: false
                );

                // Stream response
                var channel = _assistant.StreamResponseAsync(request);
                var fullResponse = "";

                await Clients.Caller.SendAsync("StreamStart");

                await foreach (var chunk in channel.ReadAllAsync())
                {
                    fullResponse += chunk;
                    await Clients.Caller.SendAsync("StreamChunk", chunk);
                }

                await Clients.Caller.SendAsync("StreamEnd", fullResponse);

                // Record telemetry
                var inputTokens = EstimateTokens(prompt + planJson);
                var outputTokens = EstimateTokens(fullResponse);
                _telemetry.RecordUsage(userId, _assistant.Capabilities.ModelName, false, inputTokens, outputTokens);

                _logger.LogInformation("[Assistant] User {UserId} sent message, received {Length} chars", userId, fullResponse.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Assistant] Error processing message");
                await Clients.Caller.SendAsync("Error", $"Error: {ex.Message}");
            }
        }

        private string CompressPlan(string planJson)
        {
            if (string.IsNullOrWhiteSpace(planJson)) return "";

            try
            {
                // Parse and extract only essential fields
                var doc = JsonDocument.Parse(planJson);
                var root = doc.RootElement;

                var compressed = new
                {
                    days = root.GetProperty("days").EnumerateArray().Select(d => new
                    {
                        day = d.GetProperty("dayNumber").GetInt32(),
                        morning = GetStringArray(d, "morning"),
                        afternoon = GetStringArray(d, "afternoon"),
                        evening = GetStringArray(d, "evening")
                    }).ToList()
                };

                return JsonSerializer.Serialize(compressed);
            }
            catch
            {
                return planJson.Length > 4000 ? planJson.Substring(0, 4000) : planJson;
            }
        }

        private string[] GetStringArray(JsonElement element, string propertyName)
        {
            try
            {
                return element.GetProperty(propertyName).EnumerateArray()
                    .Select(e => e.GetString() ?? "")
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private int EstimateTokens(string text)
        {
            // Rough estimate: 1 token ≈ 4 characters
            return string.IsNullOrWhiteSpace(text) ? 0 : text.Length / 4;
        }
    }
}
