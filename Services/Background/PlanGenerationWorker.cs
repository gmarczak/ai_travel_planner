using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using project.Models;
using project.Services;

namespace project.Services.Background
{
    public class PlanGenerationWorker : BackgroundService
    {
        private readonly ILogger<PlanGenerationWorker> _logger;
        private readonly IPlanJobQueue _queue;
        private readonly IMemoryCache _cache;
        private readonly IServiceProvider _serviceProvider;

        public const string StatusKeyPrefix = "planstatus:";

        public PlanGenerationWorker(
            ILogger<PlanGenerationWorker> logger,
            IPlanJobQueue queue,
            IMemoryCache cache,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _queue = queue;
            _cache = cache;
            _serviceProvider = serviceProvider;
        }

        public static string StateKey(string id) => $"{StatusKeyPrefix}{id}";

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PlanGenerationWorker started");
            while (!stoppingToken.IsCancellationRequested)
            {
                PlanGenerationJob job;
                try
                {
                    job = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _cache.Set(StateKey(job.PlanId), new PlanGenerationState
                {
                    Status = PlanGenerationStatus.InProgress,
                    StartedAt = DateTimeOffset.UtcNow
                }, TimeSpan.FromMinutes(40));

                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var travelService = scope.ServiceProvider.GetRequiredService<ITravelService>();

                    var plan = await travelService.GenerateTravelPlanAsync(job.Request);
                    _cache.Set(job.PlanId, plan, TimeSpan.FromMinutes(30));

                    _cache.Set(StateKey(job.PlanId), new PlanGenerationState
                    {
                        Status = PlanGenerationStatus.Completed,
                        StartedAt = DateTimeOffset.UtcNow,
                        CompletedAt = DateTimeOffset.UtcNow
                    }, TimeSpan.FromMinutes(40));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Plan generation failed for {PlanId}", job.PlanId);
                    _cache.Set(StateKey(job.PlanId), new PlanGenerationState
                    {
                        Status = PlanGenerationStatus.Failed,
                        StartedAt = DateTimeOffset.UtcNow,
                        CompletedAt = DateTimeOffset.UtcNow,
                        Error = "Failed to generate the plan. Please try again."
                    }, TimeSpan.FromMinutes(20));
                }
            }

            _logger.LogInformation("PlanGenerationWorker stopping");
        }
    }
}
