using OpenAI;
using OpenAI.Chat;

namespace project.Services
{
    /// <summary>
    /// Service to generate concise, descriptive captions for activity images using AI
    /// </summary>
    public interface IImageCaptionService
    {
        Task<Dictionary<string, string>> GenerateCaptionsAsync(IEnumerable<(string Activity, string Destination)> requests);
    }

    public class ImageCaptionService : IImageCaptionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ImageCaptionService> _logger;
        private readonly OpenAIClient? _openAIClient;

        public ImageCaptionService(IConfiguration configuration, ILogger<ImageCaptionService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var apiKey = _configuration["OpenAI:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _openAIClient = new OpenAIClient(apiKey);
            }
            else
            {
                _logger.LogWarning("OpenAI API key not configured - image captions will use fallback text");
            }
        }

        public async Task<Dictionary<string, string>> GenerateCaptionsAsync(IEnumerable<(string Activity, string Destination)> requests)
        {
            var results = new Dictionary<string, string>();
            var requestList = requests.ToList();

            if (!requestList.Any())
                return results;

            // If no API key, return truncated activity text as fallback
            if (_openAIClient == null)
            {
                foreach (var req in requestList)
                {
                    results[req.Activity] = TruncateToWords(req.Activity, 8);
                }
                return results;
            }

            try
            {
                var chatClient = _openAIClient.GetChatClient("gpt-4o-mini");
                var activitiesText = string.Join("\n", requestList.Select((r, i) => $"{i + 1}. {r.Activity} in {r.Destination}"));

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You generate short, visual image captions (max 8 words) for travel activities. Return ONLY the caption text, no quotes or extra formatting."),
                    new UserChatMessage($@"Generate concise image captions for these activities (one per line):

{activitiesText}

Examples:
- Historic Prague Castle at golden hour
- Traditional ramen shop in Tokyo
- Gondola ride through Venice canals

Return captions in order, one per line.")
                };

                var completion = await chatClient.CompleteChatAsync(messages, new ChatCompletionOptions
                {
                    MaxOutputTokenCount = 150,
                    Temperature = 0.7f
                });

                var response = completion.Value.Content[0].Text.Trim();
                var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                // Map captions back to activities
                for (int i = 0; i < requestList.Count && i < lines.Length; i++)
                {
                    var caption = lines[i].Trim().Trim('-', '•', '*', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', ' ');
                    caption = caption.Trim('"', '\'', '{', '}', '[', ']');

                    // Remove any remaining curly braces or brackets
                    caption = caption.Replace("{", "").Replace("}", "").Replace("[", "").Replace("]", "");

                    // Use caption if valid, otherwise fallback
                    results[requestList[i].Activity] = caption.Length > 0 && caption.Length <= 100
                        ? caption
                        : TruncateToWords(requestList[i].Activity, 8);
                }

                // Fill any missing with fallback
                for (int i = lines.Length; i < requestList.Count; i++)
                {
                    results[requestList[i].Activity] = TruncateToWords(requestList[i].Activity, 8);
                }

                _logger.LogInformation("Generated {Count} AI captions for images", results.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate AI captions, using fallback");
                foreach (var req in requestList)
                {
                    results[req.Activity] = TruncateToWords(req.Activity, 8);
                }
            }

            return results;
        }

        private static string TruncateToWords(string text, int maxWords)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Remove any curly braces or brackets that might be in original text
            text = text.Replace("{", "").Replace("}", "").Replace("[", "").Replace("]", "");

            var words = text.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= maxWords)
                return text;

            return string.Join(" ", words.Take(maxWords)) + "...";
        }
    }
}
