using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using project.Services.Exceptions;

namespace project.Services
{
    /// <summary>
    /// Provides resilience policies for external API calls using Polly
    /// </summary>
    public class ResiliencePolicyService
    {
        private readonly IAsyncPolicy<HttpResponseMessage> _httpRetryPolicy;
        private readonly IAsyncPolicy<HttpResponseMessage> _httpCircuitBreakerPolicy;
        private readonly IAsyncPolicy<HttpResponseMessage> _combinedPolicy;
        private readonly ILogger<ResiliencePolicyService> _logger;

        public ResiliencePolicyService(ILogger<ResiliencePolicyService> logger)
        {
            _logger = logger;

            // Retry policy: exponential backoff with jitter
            _httpRetryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r =>
                    (int)r.StatusCode >= 500 ||  // Server errors
                    r.StatusCode == System.Net.HttpStatusCode.RequestTimeout)  // Timeouts
                .Or<HttpRequestException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt =>
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
                                   TimeSpan.FromMilliseconds(new Random().Next(0, 1000));
                        _logger.LogWarning($"Retry attempt {retryAttempt} scheduled for {delay.TotalSeconds:F2} seconds");
                        return delay;
                    },
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            $"Retry {retryCount} after {timespan.TotalSeconds:F2}s. " +
                            $"Status: {outcome.Result?.StatusCode ?? System.Net.HttpStatusCode.InternalServerError}");
                    });

            // Circuit breaker: open after 5 failures in 30 seconds
            _httpCircuitBreakerPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500)
                .Or<HttpRequestException>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (outcome, timespan) =>
                    {
                        _logger.LogError(
                            $"Circuit breaker opened for {timespan.TotalSeconds:F2}s due to consecutive failures. " +
                            $"Last error: {outcome.Result?.StatusCode ?? System.Net.HttpStatusCode.InternalServerError}");
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker reset - service recovered");
                    });

            // Combine policies: retry first, then circuit breaker
            _combinedPolicy = Policy.WrapAsync(_httpRetryPolicy, _httpCircuitBreakerPolicy);
        }

        /// <summary>
        /// Execute HTTP request with retry and circuit breaker policies
        /// </summary>
        public async Task<T> ExecuteWithResilienceAsync<T>(
            Func<Task<T>> operation,
            string operationName)
        {
            try
            {
                _logger.LogInformation($"Starting resilient operation: {operationName}");
                return await operation();
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError(ex, $"Circuit breaker is open for operation: {operationName}");
                throw new AiServiceUnavailableException(
                    operationName,
                    $"Service temporarily unavailable (circuit breaker open). Please retry in a few moments.",
                    ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"HTTP error in operation: {operationName}");
                throw new AiServiceUnavailableException(
                    operationName,
                    "Failed to connect to external service. Please check your internet connection.",
                    ex);
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, $"Timeout in operation: {operationName}");
                throw new AiServiceUnavailableException(
                    operationName,
                    "Request timed out. Please try again with a simpler request.",
                    ex);
            }
            catch (TravelPlanningException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error in operation: {operationName}");
                throw new TravelPlanningException(
                    $"Unexpected error during {operationName}: {ex.Message}",
                    "UNEXPECTED_ERROR",
                    ex);
            }
        }

        /// <summary>
        /// Get HTTP retry policy for use with HttpClient
        /// </summary>
        public IAsyncPolicy<HttpResponseMessage> GetHttpPolicy() => _combinedPolicy;
    }
}
