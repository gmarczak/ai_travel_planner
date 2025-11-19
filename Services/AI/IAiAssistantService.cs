using System.Threading.Channels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace project.Services.AI
{
    /// <summary>
    /// Abstraction for AI chat assistant able to modify travel plans.
    /// </summary>
    public interface IAiAssistantService
    {
        /// <summary>
        /// Sends a user message and gets a full assistant response (non-streaming).
        /// </summary>
        Task<AiAssistantResponse> SendMessageAsync(AiAssistantRequest request);

        /// <summary>
        /// Streams an assistant response token-by-token/backpressure aware.
        /// Returns a channel the caller can read from until completion.
        /// </summary>
        ChannelReader<string> StreamResponseAsync(AiAssistantRequest request);

        /// <summary>
        /// Simple capability descriptor (e.g. model name, max tokens, supports tool calls).
        /// </summary>
        AiAssistantCapabilities Capabilities { get; }
    }

    public record AiAssistantCapabilities(string ModelName, int MaxInputTokens, bool SupportsStreaming, bool SupportsStructuredJson, bool SupportsToolCalls);

    /// <summary>
    /// User request, containing the raw prompt and current plan context (compressed).
    /// </summary>
    public record AiAssistantRequest(string UserId, string Prompt, string Destination, int Days, int Travelers, decimal Budget,
                                     IReadOnlyList<ChatMessage> History, string CompressedPlanJson, bool ForceHigherQuality = false);

    /// <summary>
    /// Structured assistant response.
    /// </summary>
    public record AiAssistantResponse(string RawText, PlanDelta? Delta, string? Explanation, bool UsedFallbackModel, string ModelUsed);

    /// <summary>
    /// A diff describing changes to apply to the plan (optional for simple Q&A).
    /// </summary>
    public record PlanDelta(IReadOnlyList<DayChange> Changes, int? TruncateToDays = null);

    public record DayChange(int Day,
                            List<string>? AddMorning = null,
                            List<string>? AddAfternoon = null,
                            List<string>? AddEvening = null,
                            List<string>? Remove = null,
                            string? Note = null);

    public enum ChatRole { User, Assistant, System }
    public record ChatMessage(ChatRole Role, string Content, string? Model = null, System.DateTime? Timestamp = null);
}
