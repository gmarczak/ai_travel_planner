using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace project.Services.AI
{
    /// <summary>
    /// Simple rate limiter for AI assistant requests per user.
    /// </summary>
    public class AssistantRateLimiter
    {
        private readonly ConcurrentDictionary<string, UserRateLimit> _userLimits = new();
        private readonly int _maxRequestsPerMinute;
        private readonly int _maxRequestsPerHour;

        public AssistantRateLimiter(int maxPerMinute = 10, int maxPerHour = 50)
        {
            _maxRequestsPerMinute = maxPerMinute;
            _maxRequestsPerHour = maxPerHour;
        }

        public async Task<RateLimitResult> CheckRateLimitAsync(string userId)
        {
            var limit = _userLimits.GetOrAdd(userId, _ => new UserRateLimit());

            lock (limit)
            {
                var now = DateTime.UtcNow;

                // Clean old entries
                limit.RequestTimestamps.RemoveAll(t => (now - t).TotalHours > 1);

                var lastMinute = limit.RequestTimestamps.FindAll(t => (now - t).TotalMinutes < 1);
                var lastHour = limit.RequestTimestamps;

                if (lastMinute.Count >= _maxRequestsPerMinute)
                {
                    var oldestInMinute = lastMinute.Min();
                    var waitTime = TimeSpan.FromMinutes(1) - (now - oldestInMinute);
                    return new RateLimitResult(false, $"Rate limit exceeded. Try again in {waitTime.TotalSeconds:F0} seconds.", waitTime);
                }

                if (lastHour.Count >= _maxRequestsPerHour)
                {
                    var oldestInHour = lastHour.Min();
                    var waitTime = TimeSpan.FromHours(1) - (now - oldestInHour);
                    return new RateLimitResult(false, $"Hourly rate limit exceeded. Try again in {waitTime.TotalMinutes:F0} minutes.", waitTime);
                }

                // Record this request
                limit.RequestTimestamps.Add(now);
                return new RateLimitResult(true, "OK", TimeSpan.Zero);
            }
        }

        public void ResetUser(string userId)
        {
            _userLimits.TryRemove(userId, out _);
        }

        private class UserRateLimit
        {
            public System.Collections.Generic.List<DateTime> RequestTimestamps { get; } = new();
        }
    }

    public record RateLimitResult(bool Allowed, string Message, TimeSpan RetryAfter);
}
