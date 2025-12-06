using System.Collections.Concurrent;

namespace project.Services
{
    /// <summary>
    /// Global rate limiter for AI API calls to control costs and prevent abuse.
    /// Uses sliding window algorithm to track request rates per user/IP.
    /// </summary>
    public class ApiRateLimiter
    {
        private readonly ConcurrentDictionary<string, Queue<DateTime>> _requestTimestamps = new();
        private readonly ILogger<ApiRateLimiter> _logger;
        private readonly SemaphoreSlim _cleanupLock = new(1, 1);
        private DateTime _lastCleanup = DateTime.UtcNow;

        // Configuration
        private readonly int _maxRequestsPerMinute;
        private readonly int _maxRequestsPerHour;
        private readonly TimeSpan _slidingWindowMinute = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _slidingWindowHour = TimeSpan.FromHours(1);

        public ApiRateLimiter(ILogger<ApiRateLimiter> logger, IConfiguration configuration)
        {
            _logger = logger;
            _maxRequestsPerMinute = configuration.GetValue<int>("RateLimit:MaxRequestsPerMinute", 10);
            _maxRequestsPerHour = configuration.GetValue<int>("RateLimit:MaxRequestsPerHour", 100);
        }

        /// <summary>
        /// Checks if the request should be allowed based on rate limits.
        /// </summary>
        /// <param name="identifier">User ID, IP address, or other unique identifier</param>
        /// <param name="apiName">Name of the API being called (for logging)</param>
        /// <returns>True if request is allowed, false if rate limit exceeded</returns>
        public async Task<bool> CheckRateLimitAsync(string identifier, string apiName = "AI")
        {
            // Periodic cleanup of old entries (every 5 minutes)
            await PeriodicCleanupAsync();

            var now = DateTime.UtcNow;
            var queue = _requestTimestamps.GetOrAdd(identifier, _ => new Queue<DateTime>());

            lock (queue)
            {
                // Remove timestamps outside sliding windows
                while (queue.Count > 0 && (now - queue.Peek()) > _slidingWindowHour)
                {
                    queue.Dequeue();
                }

                // Count requests in each window
                var requestsInLastMinute = queue.Count(ts => (now - ts) <= _slidingWindowMinute);
                var requestsInLastHour = queue.Count;

                // Check limits
                if (requestsInLastMinute >= _maxRequestsPerMinute)
                {
                    _logger.LogWarning(
                        "Rate limit exceeded for {Identifier} on {ApiName}: {Requests} requests in last minute (limit: {Limit})",
                        identifier, apiName, requestsInLastMinute, _maxRequestsPerMinute);
                    return false;
                }

                if (requestsInLastHour >= _maxRequestsPerHour)
                {
                    _logger.LogWarning(
                        "Rate limit exceeded for {Identifier} on {ApiName}: {Requests} requests in last hour (limit: {Limit})",
                        identifier, apiName, requestsInLastHour, _maxRequestsPerHour);
                    return false;
                }

                // Allow request and record timestamp
                queue.Enqueue(now);
                _logger.LogDebug(
                    "Rate limit check passed for {Identifier} on {ApiName}: {MinuteCount}/min, {HourCount}/hour",
                    identifier, apiName, requestsInLastMinute + 1, requestsInLastHour + 1);
                return true;
            }
        }

        /// <summary>
        /// Gets current request counts for an identifier (for monitoring/diagnostics)
        /// </summary>
        public (int requestsPerMinute, int requestsPerHour) GetCurrentCounts(string identifier)
        {
            if (!_requestTimestamps.TryGetValue(identifier, out var queue))
                return (0, 0);

            var now = DateTime.UtcNow;
            lock (queue)
            {
                var requestsInLastMinute = queue.Count(ts => (now - ts) <= _slidingWindowMinute);
                var requestsInLastHour = queue.Count(ts => (now - ts) <= _slidingWindowHour);
                return (requestsInLastMinute, requestsInLastHour);
            }
        }

        /// <summary>
        /// Removes old entries from memory to prevent unbounded growth
        /// </summary>
        private async Task PeriodicCleanupAsync()
        {
            if ((DateTime.UtcNow - _lastCleanup).TotalMinutes < 5)
                return;

            if (!await _cleanupLock.WaitAsync(0))
                return; // Cleanup already in progress

            try
            {
                var now = DateTime.UtcNow;
                var keysToRemove = new List<string>();

                foreach (var kvp in _requestTimestamps)
                {
                    lock (kvp.Value)
                    {
                        // Remove old timestamps
                        while (kvp.Value.Count > 0 && (now - kvp.Value.Peek()) > _slidingWindowHour)
                        {
                            kvp.Value.Dequeue();
                        }

                        // Mark empty queues for removal
                        if (kvp.Value.Count == 0)
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                    }
                }

                // Remove empty entries
                foreach (var key in keysToRemove)
                {
                    _requestTimestamps.TryRemove(key, out _);
                }

                _lastCleanup = now;
                _logger.LogDebug("Rate limiter cleanup: removed {Count} empty entries", keysToRemove.Count);
            }
            finally
            {
                _cleanupLock.Release();
            }
        }
    }
}
