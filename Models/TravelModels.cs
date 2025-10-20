using System.ComponentModel.DataAnnotations;

namespace project.Models
{
    public class TravelPlan
    {
        public int Id { get; set; }
        public string Destination { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int NumberOfTravelers { get; set; }
        public decimal Budget { get; set; }
        public string TravelPreferences { get; set; } = string.Empty;
        public string GeneratedItinerary { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // External cache id (GUID string) used to link in-memory generated plans to persisted rows
        public string? ExternalId { get; set; }

        // Foreign key to ApplicationUser (nullable for existing plans)
        public string? UserId { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Accommodations { get; set; } = new();

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Activities { get; set; } = new();

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Transportation { get; set; } = new();
    }

    public class TravelPlanRequest
    {
        [Required(ErrorMessage = "Please enter a destination")]
        [Display(Name = "Destination")]
        public string Destination { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a start date")]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(7);

        [Required(ErrorMessage = "Please select an end date")]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(14);

        [Required(ErrorMessage = "Please enter number of travelers")]
        [Range(1, 20, ErrorMessage = "Number of travelers must be between 1 and 20")]
        [Display(Name = "Number of Travelers")]
        public int NumberOfTravelers { get; set; } = 2;

        [Required(ErrorMessage = "Please enter your budget")]
        [Range(100, 100000, ErrorMessage = "Budget must be between $100 and $100,000")]
        [Display(Name = "Budget")]
        public decimal Budget { get; set; }

        // OPTIONAL FIELDS
        [Display(Name = "Travel Preferences")]
        public string? TravelPreferences { get; set; }

        [Display(Name = "Trip Type")]
        public string? TripType { get; set; }

    }

    public class TravelSuggestion
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal? EstimatedCost { get; set; }
        public string? Location { get; set; }
        public int Priority { get; set; }
    }
}