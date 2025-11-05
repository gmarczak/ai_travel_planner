using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using project.Models;
using project.Services;
using Microsoft.AspNetCore.SignalR;
using project.Hubs;

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
                    var planStatusService = scope.ServiceProvider.GetRequiredService<IPlanStatusService>();

                    // Update progress: Starting plan generation
                    await planStatusService.UpdateProgressAsync(
                        job.PlanId,
                        10,
                        "Starting plan generation...");

                    await planStatusService.UpdateProgressAsync(
                        job.PlanId,
                        20,
                        "Analyzing destination and preferences...");

                    await planStatusService.UpdateProgressAsync(
                        job.PlanId,
                        35,
                        "Discovering top attractions and places...");

                    await planStatusService.UpdateProgressAsync(
                        job.PlanId,
                        50,
                        "Gathering detailed information about each place...");

                    var plan = await travelService.GenerateTravelPlanAsync(job.Request);

                    await planStatusService.UpdateProgressAsync(
                        job.PlanId,
                        70,
                        "Building your day-by-day itinerary...");

                    await planStatusService.UpdateProgressAsync(
                        job.PlanId,
                        85,
                        "Adding accommodation and transport options...");

                    await planStatusService.UpdateProgressAsync(
                        job.PlanId,
                        95,
                        "Finalizing your travel plan...");

                    _cache.Set(job.PlanId, plan, TimeSpan.FromMinutes(30));

                    _cache.Set(StateKey(job.PlanId), new PlanGenerationState
                    {
                        Status = PlanGenerationStatus.Completed,
                        StartedAt = DateTimeOffset.UtcNow,
                        CompletedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow
                    }, TimeSpan.FromMinutes(40));

                    await planStatusService.MarkAsCompletedAsync(job.PlanId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Plan generation failed for {PlanId}", job.PlanId);

                    using var scope = _serviceProvider.CreateScope();
                    var planStatusService = scope.ServiceProvider.GetRequiredService<IPlanStatusService>();

                    _cache.Set(StateKey(job.PlanId), new PlanGenerationState
                    {
                        Status = PlanGenerationStatus.Failed,
                        StartedAt = DateTimeOffset.UtcNow,
                        CompletedAt = DateTime.UtcNow,
                        Error = "Failed to generate the plan. Please try again.",
                        CreatedAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow,
                        ErrorMessage = "Failed to generate the plan. Please try again."
                    }, TimeSpan.FromMinutes(40));

                    // Mark as failed with error message
                    await planStatusService.MarkAsFailedAsync(
                        job.PlanId,
                        ex.Message ?? "Failed to generate the plan. Please try again.");
                }
            }

            _logger.LogInformation("PlanGenerationWorker stopping");
        }
    }
}
