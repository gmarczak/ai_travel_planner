using System.Threading.Channels;
using project.Models;

namespace project.Services.Background
{
    public interface IPlanJobQueue
    {
        ValueTask EnqueueAsync(PlanGenerationJob job, CancellationToken cancellationToken = default);
        ValueTask<PlanGenerationJob> DequeueAsync(CancellationToken cancellationToken);
    }

    public record PlanGenerationJob(string PlanId, TravelPlanRequest Request, string? UserId, string? AnonymousCookieId);

    public class PlanGenerationQueue : IPlanJobQueue
    {
        private readonly Channel<PlanGenerationJob> _queue = Channel.CreateUnbounded<PlanGenerationJob>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        public ValueTask EnqueueAsync(PlanGenerationJob job, CancellationToken cancellationToken = default)
            => _queue.Writer.WriteAsync(job, cancellationToken);

        public ValueTask<PlanGenerationJob> DequeueAsync(CancellationToken cancellationToken)
            => _queue.Reader.ReadAsync(cancellationToken);
    }
}
