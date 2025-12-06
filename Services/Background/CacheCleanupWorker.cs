using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using project.Services;

namespace project.Services.Background
{
    public class CacheCleanupWorker : BackgroundService
    {
        private readonly ILogger<CacheCleanupWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(6); // Co 6 godzin

        public CacheCleanupWorker(ILogger<CacheCleanupWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CacheCleanupWorker started");

            // Initial delay before first cleanup
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_cleanupInterval, stoppingToken);

                    using var scope = _serviceProvider.CreateScope();
                    var cacheService = scope.ServiceProvider.GetRequiredService<IAiCacheService>();

                    var deletedCount = await cacheService.CleanupExpiredCacheAsync();

                    if (deletedCount > 0)
                    {
                        _logger.LogInformation("Cleaned up {Count} expired cache entries", deletedCount);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("CacheCleanupWorker stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during cache cleanup");
                }
            }
        }
    }
}
