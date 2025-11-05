using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using project.Data;
using project.Models;
using project.Hubs;

namespace project.Services
{
    public interface IPlanStatusService
    {
        Task<PlanGenerationState> CreateStatusAsync(string externalId, string destination, int travelers, int days, string? userId = null, string? anonId = null);
        Task UpdateProgressAsync(string externalId, int progressPercent, string currentStep);
        Task MarkAsCompletedAsync(string externalId, int? travelPlanId = null);
        Task MarkAsFailedAsync(string externalId, string errorMessage);
        Task<PlanGenerationState?> GetStatusAsync(string externalId);
        Task<List<PlanGenerationState>> GetUserStatusesAsync(string? userId = null, string? anonId = null, int limit = 10);
        Task<int> CleanupOldStatusesAsync(int daysOld = 30);
    }

    public class PlanStatusService : IPlanStatusService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<PlanGenerationHub> _hubContext;
        private readonly ILogger<PlanStatusService> _logger;

        public PlanStatusService(
            IServiceScopeFactory scopeFactory,
            IHubContext<PlanGenerationHub> hubContext,
            ILogger<PlanStatusService> logger)
        {
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<PlanGenerationState> CreateStatusAsync(
            string externalId,
            string destination,
            int travelers,
            int days,
            string? userId = null,
            string? anonId = null)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var state = new PlanGenerationState
            {
                ExternalId = externalId,
                Status = PlanGenerationStatus.Queued,
                ProgressPercent = 0,
                CurrentStep = "Initializing...",
                Destination = destination,
                Travelers = travelers,
                Days = days,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                UserId = userId,
                AnonymousCookieId = anonId
            };

            context.PlanGenerationStates.Add(state);
            await context.SaveChangesAsync();

            _logger.LogInformation("Created plan status for {ExternalId}, destination: {Destination}",
                externalId, destination);

            // Notify via SignalR
            await _hubContext.Clients.Group($"plan_{externalId}")
                .SendAsync("StatusUpdate", new
                {
                    planId = externalId,
                    status = state.Status.ToString(),
                    progress = state.ProgressPercent,
                    message = state.CurrentStep
                });

            return state;
        }

        public async Task UpdateProgressAsync(string externalId, int progressPercent, string currentStep)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var state = await context.PlanGenerationStates
                .FirstOrDefaultAsync(s => s.ExternalId == externalId);

            if (state == null)
            {
                _logger.LogWarning("Plan status not found for {ExternalId}", externalId);
                return;
            }

            state.Status = PlanGenerationStatus.InProgress;
            state.ProgressPercent = progressPercent;
            state.CurrentStep = currentStep;
            state.LastUpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();

            _logger.LogInformation("Updated progress for {ExternalId}: {Progress}% - {Step}",
                externalId, progressPercent, currentStep);

            // Notify via SignalR
            await _hubContext.Clients.Group($"plan_{externalId}")
                .SendAsync("ProgressUpdate", new
                {
                    planId = externalId,
                    status = state.Status.ToString(),
                    progress = state.ProgressPercent,
                    message = state.CurrentStep
                });
        }

        public async Task MarkAsCompletedAsync(string externalId, int? travelPlanId = null)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var state = await context.PlanGenerationStates
                .FirstOrDefaultAsync(s => s.ExternalId == externalId);

            if (state == null)
            {
                _logger.LogWarning("Plan status not found for {ExternalId}", externalId);
                return;
            }

            state.Status = PlanGenerationStatus.Completed;
            state.ProgressPercent = 100;
            state.CurrentStep = "Completed successfully!";
            state.CompletedAt = DateTime.UtcNow;
            state.LastUpdatedAt = DateTime.UtcNow;
            state.TravelPlanId = travelPlanId;

            await context.SaveChangesAsync();

            _logger.LogInformation("Marked plan {ExternalId} as completed", externalId);

            // Notify via SignalR
            await _hubContext.Clients.Group($"plan_{externalId}")
                .SendAsync("PlanCompleted", new
                {
                    planId = externalId,
                    status = "Completed",
                    progress = 100,
                    message = "Plan generated successfully!",
                    travelPlanId = travelPlanId
                });
        }

        public async Task MarkAsFailedAsync(string externalId, string errorMessage)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var state = await context.PlanGenerationStates
                .FirstOrDefaultAsync(s => s.ExternalId == externalId);

            if (state == null)
            {
                _logger.LogWarning("Plan status not found for {ExternalId}", externalId);
                return;
            }

            state.Status = PlanGenerationStatus.Failed;
            state.CurrentStep = "Failed";
            state.ErrorMessage = errorMessage;
            state.CompletedAt = DateTime.UtcNow;
            state.LastUpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();

            _logger.LogError("Marked plan {ExternalId} as failed: {Error}", externalId, errorMessage);

            // Notify via SignalR
            await _hubContext.Clients.Group($"plan_{externalId}")
                .SendAsync("PlanFailed", new
                {
                    planId = externalId,
                    status = "Failed",
                    message = errorMessage
                });
        }

        public async Task<PlanGenerationState?> GetStatusAsync(string externalId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await context.PlanGenerationStates
                .FirstOrDefaultAsync(s => s.ExternalId == externalId);
        }

        public async Task<List<PlanGenerationState>> GetUserStatusesAsync(
            string? userId = null,
            string? anonId = null,
            int limit = 10)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var query = context.PlanGenerationStates.AsQueryable();

            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(s => s.UserId == userId);
            }
            else if (!string.IsNullOrEmpty(anonId))
            {
                query = query.Where(s => s.AnonymousCookieId == anonId);
            }
            else
            {
                return new List<PlanGenerationState>();
            }

            return await query
                .OrderByDescending(s => s.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<int> CleanupOldStatusesAsync(int daysOld = 30)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);

            var oldStatuses = await context.PlanGenerationStates
                .Where(s => s.CreatedAt < cutoffDate)
                .Where(s => s.Status == PlanGenerationStatus.Completed || s.Status == PlanGenerationStatus.Failed)
                .ToListAsync();

            if (oldStatuses.Any())
            {
                context.PlanGenerationStates.RemoveRange(oldStatuses);
                await context.SaveChangesAsync();

                _logger.LogInformation("Cleaned up {Count} old plan statuses", oldStatuses.Count);
            }

            return oldStatuses.Count;
        }
    }
}
