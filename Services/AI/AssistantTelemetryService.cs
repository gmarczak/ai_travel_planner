using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace project.Services.AI
{
    /// <summary>
    /// Tracks AI assistant usage metrics: model calls, escalations, tokens (estimated).
    /// </summary>
    public class AssistantTelemetryService
    {
        private readonly ILogger<AssistantTelemetryService> _logger;
        private readonly ConcurrentBag<UsageMetric> _metrics = new();

        public AssistantTelemetryService(ILogger<AssistantTelemetryService> logger)
        {
            _logger = logger;
        }

        public void RecordUsage(string userId, string modelUsed, bool wasFallback, int estimatedInputTokens, int estimatedOutputTokens)
        {
            var metric = new UsageMetric
            {
                Timestamp = DateTime.UtcNow,
                UserId = userId,
                ModelUsed = modelUsed,
                WasFallback = wasFallback,
                EstimatedInputTokens = estimatedInputTokens,
                EstimatedOutputTokens = estimatedOutputTokens
            };

            _metrics.Add(metric);

            _logger.LogInformation(
                "[AI Assistant] User: {UserId}, Model: {Model}, Fallback: {Fallback}, Tokens: ~{Tokens}",
                userId, modelUsed, wasFallback, estimatedInputTokens + estimatedOutputTokens
            );
        }

        public AssistantUsageStats GetStats(string? userId = null, DateTime? since = null)
        {
            var filtered = _metrics.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(userId))
                filtered = filtered.Where(m => m.UserId == userId);

            if (since.HasValue)
                filtered = filtered.Where(m => m.Timestamp >= since.Value);

            var list = filtered.ToList();

            return new AssistantUsageStats
            {
                TotalCalls = list.Count,
                FallbackCalls = list.Count(m => m.WasFallback),
                TotalEstimatedTokens = list.Sum(m => m.EstimatedInputTokens + m.EstimatedOutputTokens),
                ModelBreakdown = list.GroupBy(m => m.ModelUsed)
                    .ToDictionary(g => g.Key, g => g.Count()),
                EscalationRate = list.Any() ? (double)list.Count(m => m.WasFallback) / list.Count * 100 : 0
            };
        }

        public void ClearOldMetrics(TimeSpan olderThan)
        {
            var cutoff = DateTime.UtcNow - olderThan;
            var toKeep = _metrics.Where(m => m.Timestamp >= cutoff).ToList();
            _metrics.Clear();
            foreach (var m in toKeep)
                _metrics.Add(m);

            _logger.LogInformation("[AI Assistant] Cleared metrics older than {Cutoff}", cutoff);
        }

        private class UsageMetric
        {
            public DateTime Timestamp { get; set; }
            public string UserId { get; set; } = "";
            public string ModelUsed { get; set; } = "";
            public bool WasFallback { get; set; }
            public int EstimatedInputTokens { get; set; }
            public int EstimatedOutputTokens { get; set; }
        }
    }

    public class AssistantUsageStats
    {
        public int TotalCalls { get; set; }
        public int FallbackCalls { get; set; }
        public int TotalEstimatedTokens { get; set; }
        public Dictionary<string, int> ModelBreakdown { get; set; } = new();
        public double EscalationRate { get; set; }
    }
}
