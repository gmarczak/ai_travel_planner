namespace project.Models
{
    /// <summary>
    /// Represents the persistent state of a travel plan generation process.
    /// Allows users to track progress and resume if they close the page.
    /// </summary>
    public class PlanGenerationState
    {
        public int Id { get; set; }

        /// <summary>
        /// External ID used in URLs (GUID)
        /// </summary>
        public string ExternalId { get; set; } = string.Empty;

        /// <summary>
        /// Current status of plan generation
        /// </summary>
        public PlanGenerationStatus Status { get; set; }

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public int ProgressPercent { get; set; }

        /// <summary>
        /// Current step description (e.g., "Generating place list...")
        /// </summary>
        public string? CurrentStep { get; set; }

        /// <summary>
        /// Destination being planned
        /// </summary>
        public string Destination { get; set; } = string.Empty;

        /// <summary>
        /// Number of travelers
        /// </summary>
        public int Travelers { get; set; }

        /// <summary>
        /// Trip duration in days
        /// </summary>
        public int Days { get; set; }

        /// <summary>
        /// When generation started
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When generation completed (success or failure)
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime LastUpdatedAt { get; set; }

        /// <summary>
        /// Error message if failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// User ID if authenticated
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Anonymous cookie ID if not authenticated
        /// </summary>
        public string? AnonymousCookieId { get; set; }

        /// <summary>
        /// ID of the completed TravelPlan (if successful)
        /// </summary>
        public int? TravelPlanId { get; set; }

        // Legacy properties for backward compatibility
        public DateTimeOffset? StartedAt { get; set; }
        public string? Error { get; set; }
    }
}

