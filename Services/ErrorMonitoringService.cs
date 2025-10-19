namespace project.Services
{
    public interface IErrorMonitoringService
    {
        Task LogErrorAsync(Exception exception, string context = "", Dictionary<string, object>? additionalData = null);
        Task LogWarningAsync(string message, string context = "", Dictionary<string, object>? additionalData = null);
        Task LogInfoAsync(string message, string context = "", Dictionary<string, object>? additionalData = null);
    }

    public class ErrorMonitoringService : IErrorMonitoringService
    {
        private readonly ILogger<ErrorMonitoringService> _logger;
        private readonly IConfiguration _configuration;

        public ErrorMonitoringService(ILogger<ErrorMonitoringService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task LogErrorAsync(Exception exception, string context = "", Dictionary<string, object>? additionalData = null)
        {
            var errorData = new
            {
                Message = exception.Message,
                StackTrace = exception.StackTrace,
                Source = exception.Source,
                Context = context,
                Timestamp = DateTime.UtcNow,
                AdditionalData = additionalData,
                UserAgent = GetUserAgent(),
                IpAddress = GetClientIpAddress()
            };

            _logger.LogError(exception, "Error occurred in context: {Context}. Data: {@ErrorData}", context, errorData);
            // SEND TO EXTERNAL SERVICE (SIMULATED)
            await SendToExternalServiceAsync("error", errorData);
        }

        public async Task LogWarningAsync(string message, string context = "", Dictionary<string, object>? additionalData = null)
        {
            var warningData = new
            {
                Message = message,
                Context = context,
                Timestamp = DateTime.UtcNow,
                AdditionalData = additionalData,
                UserAgent = GetUserAgent(),
                IpAddress = GetClientIpAddress()
            };

            _logger.LogWarning("Warning in context: {Context}. Data: {@WarningData}", context, warningData);
            // SEND WARNING (SIMULATED)
            await SendToExternalServiceAsync("warning", warningData);
        }

        public async Task LogInfoAsync(string message, string context = "", Dictionary<string, object>? additionalData = null)
        {
            var infoData = new
            {
                Message = message,
                Context = context,
                Timestamp = DateTime.UtcNow,
                AdditionalData = additionalData
            };

            _logger.LogInformation("Info in context: {Context}. Data: {@InfoData}", context, infoData);
            // SEND INFO (SIMULATED)
            await SendToExternalServiceAsync("info", infoData);
        }

        private string GetUserAgent()
        {
            // USER AGENT: UNKNOWN (PLACEHOLDER)
            return "Unknown";
        }

        private string GetClientIpAddress()
        {
            // CLIENT IP: UNKNOWN (PLACEHOLDER)
            return "Unknown";
        }

        private async Task SendToExternalServiceAsync(string level, object data)
        {
            try
            {
                // EXTERNAL INTEGRATION (SENTRY, APP INSIGHTS, ETC.) - SIMULATED
                // SIMULATED ASYNC OPERATION
                await Task.Delay(1);

                // SAVE TO FILE FOR BASIC MONITORING
                var logFile = Path.Combine("logs", $"errors-{DateTime.UtcNow:yyyy-MM-dd}.json");
                var logEntry = new
                {
                    Level = level,
                    Data = data,
                    Timestamp = DateTime.UtcNow
                };

                // Ensure logs directory exists
                Directory.CreateDirectory("logs");

                // Append to daily log file
                var json = System.Text.Json.JsonSerializer.Serialize(logEntry, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.AppendAllTextAsync(logFile, json + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Don't let logging errors crash the application
                _logger.LogError(ex, "Failed to send error to external service");
            }
        }
    }
}