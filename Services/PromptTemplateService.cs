namespace project.Services
{
    /// <summary>
    /// Loads AI prompt templates from files and provides methods to populate them with data.
    /// Allows for easy modification of prompts without code recompilation.
    /// </summary>
    public class PromptTemplateService
    {
        private readonly ILogger<PromptTemplateService> _logger;
        private readonly string _promptsPath;
        private readonly Dictionary<string, string> _templateCache = new();

        public PromptTemplateService(ILogger<PromptTemplateService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _promptsPath = Path.Combine(env.ContentRootPath, "Resources", "Prompts");
        }

        /// <summary>
        /// Loads a prompt template from file (with caching)
        /// </summary>
        private string LoadTemplate(string templateName)
        {
            if (_templateCache.TryGetValue(templateName, out var cached))
                return cached;

            var filePath = Path.Combine(_promptsPath, templateName);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Prompt template not found: {TemplateName}", templateName);
                return string.Empty;
            }

            var content = File.ReadAllText(filePath);
            _templateCache[templateName] = content;
            return content;
        }

        /// <summary>
        /// Replaces placeholders in template with actual values
        /// </summary>
        private string PopulateTemplate(string template, Dictionary<string, string> values)
        {
            foreach (var kvp in values)
            {
                template = template.Replace($"{{{kvp.Key}}}", kvp.Value);
            }
            return template;
        }

        public string GetPlaceListPrompt(string destination, int days, int travelers, string tripType, string preferences)
        {
            var template = LoadTemplate("PlaceListPrompt.txt");
            var placesCount = days * 4;

            return PopulateTemplate(template, new Dictionary<string, string>
            {
                { "PLACES_COUNT", placesCount.ToString() },
                { "DAYS", days.ToString() },
                { "DESTINATION", destination },
                { "TRAVELERS", travelers.ToString() },
                { "TRIP_TYPE", tripType },
                { "PREFERENCES", preferences }
            });
        }

        public string GetPlaceDetailsPrompt(string placeName, string destination)
        {
            var template = LoadTemplate("PlaceDetailsPrompt.txt");

            return PopulateTemplate(template, new Dictionary<string, string>
            {
                { "PLACE_NAME", placeName },
                { "DESTINATION", destination }
            });
        }

        public string GetTravelPlanPrompt(
            string destination,
            DateTime startDate,
            DateTime endDate,
            int travelers,
            decimal budget,
            string interests,
            string tripType,
            string preferences)
        {
            var template = LoadTemplate("TravelPlanPrompt.txt");
            var days = (endDate - startDate).Days + 1;
            var budgetPerDay = budget > 0 && travelers > 0 && days > 0
                ? (budget / travelers / days).ToString("F0")
                : "0";

            return PopulateTemplate(template, new Dictionary<string, string>
            {
                { "DAYS", days.ToString() },
                { "DESTINATION", destination },
                { "START_DATE", startDate.ToString("MMM dd, yyyy") },
                { "END_DATE", endDate.ToString("MMM dd, yyyy") },
                { "TRAVELERS", travelers.ToString() },
                { "BUDGET", budget.ToString("F0") },
                { "BUDGET_PER_DAY", budgetPerDay },
                { "INTERESTS", interests },
                { "TRIP_TYPE", tripType },
                { "PREFERENCES", preferences },
                { "DAY1_DATE", startDate.ToString("MMM dd, yyyy") }
            });
        }

        public string GetDestinationSuggestionsPrompt(string query)
        {
            var template = LoadTemplate("DestinationSuggestionsPrompt.txt");

            return PopulateTemplate(template, new Dictionary<string, string>
            {
                { "QUERY", query }
            });
        }
    }
}
