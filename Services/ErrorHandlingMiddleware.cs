using System.Text.Json;
using project.Services.Exceptions;

namespace project.Services
{
    /// <summary>
    /// Global error handling middleware with structured error responses
    /// </summary>
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(
            RequestDelegate next,
            ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IErrorMonitoringService errorMonitoring)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in middleware");
                await HandleExceptionAsync(context, ex, errorMonitoring);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception, IErrorMonitoringService errorMonitoring)
        {
            context.Response.ContentType = "application/json";
            var response = new ErrorResponse();
            var additionalData = new Dictionary<string, object>
            {
                { "Path", context.Request.Path },
                { "Method", context.Request.Method }
            };

            switch (exception)
            {
                case AllAiProvidersFailedException allEx:
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    var failedProviders = allEx.FailedProviders?
                        .Select(fp => new { Provider = fp.ProviderName, Error = fp.Exception.Message })
                        .Cast<object>()
                        .ToList() ?? new List<object>();
                    response = new ErrorResponse
                    {
                        ErrorCode = "ALL_PROVIDERS_FAILED",
                        Message = "All AI providers are currently unavailable. Please try again later.",
                        Details = new Dictionary<string, object>
                        {
                            { "FailedProviders", failedProviders }
                        }
                    };
                    break;

                case AiServiceUnavailableException aiex:
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    response = new ErrorResponse
                    {
                        ErrorCode = "AI_SERVICE_UNAVAILABLE",
                        Message = aiex.Message,
                        Details = new Dictionary<string, object>
                        {
                            { "Provider", aiex.ProviderName ?? "Unknown" },
                            { "RetryAttempts", aiex.RetryCount ?? 0 }
                        }
                    };
                    break;

                case RateLimitExceededException rlEx:
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    if (rlEx.RetryAfter.HasValue)
                    {
                        context.Response.Headers["Retry-After"] = rlEx.RetryAfter.Value.ToUniversalTime().ToString("O");
                    }
                    response = new ErrorResponse
                    {
                        ErrorCode = "RATE_LIMIT_EXCEEDED",
                        Message = rlEx.Message,
                        Details = new Dictionary<string, object>
                        {
                            { "RetryAfter", rlEx.RetryAfter?.ToUniversalTime() ?? DateTime.UtcNow.AddSeconds(60) },
                            { "RemainingRequests", rlEx.RemainingRequests ?? 0 }
                        }
                    };
                    break;

                case InvalidAiResponseException respEx:
                    context.Response.StatusCode = StatusCodes.Status502BadGateway;
                    response = new ErrorResponse
                    {
                        ErrorCode = "INVALID_AI_RESPONSE",
                        Message = "AI service returned an unexpected response format.",
                        Details = new Dictionary<string, object>()
                    };
                    additionalData["RawResponseLength"] = respEx.RawResponse?.Length ?? 0;
                    break;

                case ApiConfigurationException cfgEx:
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    response = new ErrorResponse
                    {
                        ErrorCode = "CONFIGURATION_ERROR",
                        Message = "The application is not properly configured.",
                        Details = new Dictionary<string, object>
                        {
                            { "MissingKey", cfgEx.MissingConfigKey ?? "Unknown" }
                        }
                    };
                    break;

                case TravelPlanningException tpex:
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    response = new ErrorResponse
                    {
                        ErrorCode = tpex.ErrorCode ?? "TRAVEL_PLANNING_ERROR",
                        Message = tpex.Message,
                        Details = tpex.Context ?? new Dictionary<string, object>()
                    };
                    if (!string.IsNullOrEmpty(tpex.ErrorCode))
                    {
                        additionalData["ErrorCode"] = tpex.ErrorCode;
                    }
                    break;

                case ArgumentException argEx:
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    response = new ErrorResponse
                    {
                        ErrorCode = "INVALID_ARGUMENT",
                        Message = argEx.Message,
                        Details = new Dictionary<string, object>()
                    };
                    break;

                case NotFound404Exception notFoundEx:
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    response = new ErrorResponse
                    {
                        ErrorCode = "NOT_FOUND",
                        Message = "The requested resource was not found.",
                        Details = new Dictionary<string, object>()
                    };
                    break;

                default:
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    response = new ErrorResponse
                    {
                        ErrorCode = "INTERNAL_SERVER_ERROR",
                        Message = "An unexpected error occurred. Please try again later.",
                        Details = new Dictionary<string, object>()
                    };
                    additionalData["ExceptionType"] = exception.GetType().Name;
                    break;
            }

            // Log error with additional context
            await errorMonitoring.LogErrorAsync(
                exception,
                context.Request.Path,
                additionalData);

            // Write error response
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await context.Response.WriteAsync(json);
        }

        public class ErrorResponse
        {
            public string ErrorCode { get; set; } = "UNKNOWN_ERROR";
            public string Message { get; set; } = "An error occurred";
            public Dictionary<string, object> Details { get; set; } = new();
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        }
    }

    // Custom exception for 404 responses
    public class NotFound404Exception : Exception
    {
        public NotFound404Exception(string message) : base(message) { }
    }

    /// <summary>
    /// Extension methods to register error handling middleware
    /// </summary>
    public static class ErrorHandlingExtensions
    {
        public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ErrorHandlingMiddleware>();
        }
    }
}
