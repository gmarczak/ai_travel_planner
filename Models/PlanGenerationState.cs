namespace project.Models
{
    public class PlanGenerationState
    {
        public PlanGenerationStatus Status { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string? Error { get; set; }
    }
}
