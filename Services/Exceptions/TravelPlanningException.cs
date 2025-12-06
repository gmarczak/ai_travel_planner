namespace project.Services.Exceptions
{
    /// <summary>
    /// Base exception for all travel planning related errors
    /// </summary>
    public class TravelPlanningException : Exception
    {
        public string? ErrorCode { get; set; }
        public Dictionary<string, object>? Context { get; set; }

        public TravelPlanningException(string message, string? errorCode = null, Exception? innerException = null, Dictionary<string, object>? context = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            Context = context;
        }
    }

    /// <summary>
    /// Thrown when an AI service is unavailable or unreachable
    /// </summary>
    public class AiServiceUnavailableException : TravelPlanningException
    {
        public string? ProviderName { get; set; }
        public int? RetryCount { get; set; }

        public AiServiceUnavailableException(
            string providerName,
            string message,
            Exception? innerException = null,
            int retryCount = 0,
            Dictionary<string, object>? context = null)
            : base(message, "AI_SERVICE_UNAVAILABLE", innerException, context)
        {
            ProviderName = providerName;
            RetryCount = retryCount;
        }
    }

    /// <summary>
    /// Thrown when API rate limit is exceeded
    /// </summary>
    public class RateLimitExceededException : TravelPlanningException
    {
        public DateTime? RetryAfter { get; set; }
        public long? RemainingRequests { get; set; }

        public RateLimitExceededException(
            string message,
            DateTime? retryAfter = null,
            long? remainingRequests = null,
            Exception? innerException = null,
            Dictionary<string, object>? context = null)
            : base(message, "RATE_LIMIT_EXCEEDED", innerException, context)
        {
            RetryAfter = retryAfter;
            RemainingRequests = remainingRequests;
        }
    }

    /// <summary>
    /// Thrown when API configuration is missing or invalid
    /// </summary>
    public class ApiConfigurationException : TravelPlanningException
    {
        public string? MissingConfigKey { get; set; }

        public ApiConfigurationException(
            string message,
            string? missingConfigKey = null,
            Exception? innerException = null,
            Dictionary<string, object>? context = null)
            : base(message, "API_CONFIGURATION_ERROR", innerException, context)
        {
            MissingConfigKey = missingConfigKey;
        }
    }

    /// <summary>
    /// Thrown when response parsing from AI service fails
    /// </summary>
    public class InvalidAiResponseException : TravelPlanningException
    {
        public string? RawResponse { get; set; }

        public InvalidAiResponseException(
            string message,
            string? rawResponse = null,
            Exception? innerException = null,
            Dictionary<string, object>? context = null)
            : base(message, "INVALID_AI_RESPONSE", innerException, context)
        {
            RawResponse = rawResponse;
        }
    }

    /// <summary>
    /// Thrown when all AI providers fail after retries
    /// </summary>
    public class AllAiProvidersFailedException : TravelPlanningException
    {
        public List<(string ProviderName, Exception Exception)> FailedProviders { get; set; }

        public AllAiProvidersFailedException(
            List<(string, Exception)> failedProviders,
            string message = "All AI providers failed to process request",
            Dictionary<string, object>? context = null)
            : base(message, "ALL_PROVIDERS_FAILED", null, context)
        {
            FailedProviders = failedProviders;
        }
    }

    /// <summary>
    /// Thrown when database operation fails
    /// </summary>
    public class DatabaseOperationException : TravelPlanningException
    {
        public DatabaseOperationException(
            string message,
            Exception? innerException = null,
            Dictionary<string, object>? context = null)
            : base(message, "DATABASE_OPERATION_FAILED", innerException, context)
        {
        }
    }

    /// <summary>
    /// Thrown when resource is not found
    /// </summary>
    public class ResourceNotFoundException : TravelPlanningException
    {
        public string? ResourceType { get; set; }
        public string? ResourceId { get; set; }

        public ResourceNotFoundException(
            string resourceType,
            string resourceId,
            Dictionary<string, object>? context = null)
            : base($"{resourceType} with ID '{resourceId}' not found", "RESOURCE_NOT_FOUND", null, context)
        {
            ResourceType = resourceType;
            ResourceId = resourceId;
        }
    }
}
