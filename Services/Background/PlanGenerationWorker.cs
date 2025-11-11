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
                    var imageService = scope.ServiceProvider.GetRequiredService<IImageService>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<project.Data.ApplicationDbContext>();

                    // Update progress: Starting plan generation
                    // Batched progress updates (reduce DB writes for performance)
                    await planStatusService.UpdateProgressAsync(
                        job.PlanId,
                        25,
                        "Analyzing destination and discovering attractions...");

                    await planStatusService.UpdateProgressAsync(
                        job.PlanId,
                        50,
                        "Generating itinerary with AI...");

                    var plan = await travelService.GenerateTravelPlanAsync(job.Request);

                    await planStatusService.UpdateProgressAsync(
                        job.PlanId,
                        75,
                        "Finalizing your travel plan...");

                    // Fetch destination image from Unsplash (with cache and fallback)
                    try
                    {
                        plan.DestinationImageUrl = await imageService.GetDestinationImageAsync(plan.Destination);
                        _logger.LogInformation("Fetched image for {Destination}: {ImageUrl}", plan.Destination, plan.DestinationImageUrl ?? "null");
                    }
                    catch (Exception imgEx)
                    {
                        _logger.LogWarning(imgEx, "Failed to fetch image for {Destination}", plan.Destination);
                        // Continue without image - not critical
                    }

                    // Save plan to database with image URL
                    try
                    {
                        var dbPlan = new project.Models.TravelPlan
                        {
                            Destination = plan.Destination,
                            StartDate = plan.StartDate,
                            EndDate = plan.EndDate,
                            NumberOfTravelers = plan.NumberOfTravelers,
                            Budget = plan.Budget,
                            TravelPreferences = plan.TravelPreferences ?? string.Empty,
                            GeneratedItinerary = plan.GeneratedItinerary ?? string.Empty,
                            DestinationImageUrl = plan.DestinationImageUrl,
                            CreatedAt = DateTime.UtcNow,
                            UserId = job.UserId,
                            AnonymousCookieId = job.AnonymousCookieId,
                            ExternalId = job.PlanId,
                            Accommodations = plan.Accommodations,
                            Activities = plan.Activities,
                            Transportation = plan.Transportation
                        };
                        dbContext.TravelPlans.Add(dbPlan);
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("Plan auto-saved to DB: id={PlanId}, userId={UserId}, anonId={AnonId}, imageUrl={ImageUrl}", 
                            job.PlanId, job.UserId ?? "null", job.AnonymousCookieId ?? "null", plan.DestinationImageUrl ?? "null");
                    }
                    catch (Exception dbEx)
                    {
                        _logger.LogError(dbEx, "Failed to save plan to DB for {PlanId}", job.PlanId);
                        // Continue - plan is still in cache
                    }

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
