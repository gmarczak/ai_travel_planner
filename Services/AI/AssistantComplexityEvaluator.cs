using System.Text.RegularExpressions;

namespace project.Services.AI
{
    /// <summary>
    /// Heuristic based complexity evaluator deciding whether to escalate to higher quality model (gpt-4.1-mini).
    /// </summary>
    public static class AssistantComplexityEvaluator
    {
        private static readonly Regex ComplexRegex = new(
            pattern: @"(optymalizuj|reorganizuj|przeorganizuj|zminimalizuj|logistyka|routing|kolejność|harmonogram|czas przejazdu|reduce travel time|optimize route|reschedule)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static bool IsComplex(string prompt, int mentionedDaysCount = 0)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return false;
            // Many day references
            bool manyDays = mentionedDaysCount >= 3;
            bool hasKeywords = ComplexRegex.IsMatch(prompt);
            // Long prompt length threshold
            bool longPrompt = prompt.Length > 400;
            return hasKeywords || manyDays || longPrompt;
        }
    }
}
