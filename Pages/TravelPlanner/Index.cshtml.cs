using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using project.Models;
using project.Services;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using project.Services.Background;
using System.Security.Claims;

namespace project.Pages.TravelPlanner
{
    public class IndexModel : PageModel
    {
        private readonly ITravelService _travelService;
        private readonly ILogger<IndexModel> _logger;
        private readonly IMemoryCache _cache;
        private readonly IPlanJobQueue _queue;
        private readonly IPlanStatusService _planStatusService;

        public IndexModel(
            ITravelService travelService,
            ILogger<IndexModel> logger,
            IMemoryCache cache,
            IPlanJobQueue queue,
            IPlanStatusService planStatusService)
        {
            _travelService = travelService;
            _logger = logger;
            _cache = cache;
            _queue = queue;
            _planStatusService = planStatusService;
        }

        [BindProperty]
        public TravelPlanRequest TravelRequest { get; set; } = new();

        public void OnGet(string? destination)
        {
            // SET DEFAULT VALUES
            TravelRequest.StartDate = DateTime.Today.AddDays(14);
            // Default trip length: 3 days (inclusive)
            TravelRequest.EndDate = TravelRequest.StartDate.AddDays(2);
            TravelRequest.NumberOfTravelers = 2;

            // SET DESTINATION FROM QUERY STRING IF PROVIDED
            if (!string.IsNullOrWhiteSpace(destination))
            {
                TravelRequest.Destination = destination;
            }
        }

        public IActionResult OnGetDestinationSuggestions(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return new JsonResult(new string[] { });
            }

            // Popular destinations database
            var destinations = new[]
            {
                // Europe
                "Paris, France", "London, United Kingdom", "Rome, Italy", "Barcelona, Spain",
                "Amsterdam, Netherlands", "Prague, Czech Republic", "Vienna, Austria",
                "Budapest, Hungary", "Lisbon, Portugal", "Berlin, Germany", "Athens, Greece",
                "Dublin, Ireland", "Edinburgh, Scotland", "Copenhagen, Denmark", "Stockholm, Sweden",
                "Krakow, Poland", "Venice, Italy", "Florence, Italy", "Santorini, Greece",
                
                // Asia
                "Tokyo, Japan", "Kyoto, Japan", "Seoul, South Korea", "Bangkok, Thailand",
                "Singapore", "Hong Kong", "Dubai, UAE", "Bali, Indonesia", "Phuket, Thailand",
                "Mumbai, India", "Delhi, India", "Hanoi, Vietnam", "Ho Chi Minh City, Vietnam",
                "Manila, Philippines", "Taipei, Taiwan", "Shanghai, China", "Beijing, China",
                
                // Americas
                "New York City, USA", "Los Angeles, USA", "San Francisco, USA", "Las Vegas, USA",
                "Miami, USA", "Chicago, USA", "Boston, USA", "Seattle, USA", "Austin, USA",
                "Toronto, Canada", "Vancouver, Canada", "Montreal, Canada",
                "Mexico City, Mexico", "Cancun, Mexico", "Buenos Aires, Argentina",
                "Rio de Janeiro, Brazil", "Lima, Peru", "Bogota, Colombia",
                
                // Oceania & Others
                "Sydney, Australia", "Melbourne, Australia", "Auckland, New Zealand",
                "Queenstown, New Zealand", "Fiji", "Bora Bora, French Polynesia",
                
                // Africa & Middle East
                "Cape Town, South Africa", "Marrakech, Morocco", "Cairo, Egypt",
                "Tel Aviv, Israel", "Istanbul, Turkey"
            };

            var filtered = destinations
                .Where(d => d.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.IndexOf(query, StringComparison.OrdinalIgnoreCase)) // Match at start first
                .Take(8)
                .ToArray();

            return new JsonResult(filtered);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation("Processing travel plan request for destination: {Destination}", TravelRequest.Destination);

            // NORMALIZE OPTIONAL FIELDS
            if (string.IsNullOrWhiteSpace(TravelRequest.TravelPreferences))
                TravelRequest.TravelPreferences = null;
            if (string.IsNullOrWhiteSpace(TravelRequest.TripType))
                TravelRequest.TripType = null;
            if (string.IsNullOrWhiteSpace(TravelRequest.TransportMode))
                TravelRequest.TransportMode = null;
            // NOTE: Do NOT null DepartureLocation here - it's required and needs validation first

            // INTERESTS REMOVED FROM FORM

            // DATE VALIDATION
            if (TravelRequest.StartDate < DateTime.Today)
            {
                ModelState.AddModelError(nameof(TravelRequest.StartDate), "Start date cannot be in the past.");
            }

            if (TravelRequest.EndDate <= TravelRequest.StartDate)
            {
                ModelState.AddModelError(nameof(TravelRequest.EndDate), "End date must be after start date.");
            }

            // BUDGET VALIDATION (handle nullable budget)
            var days = (TravelRequest.EndDate - TravelRequest.StartDate).Days + 1;
            var budgetValue = TravelRequest.Budget ?? 0m;
            if (TravelRequest.Budget.HasValue && budgetValue > 0)
            {
                var budgetPerPersonPerDay = budgetValue / TravelRequest.NumberOfTravelers / days;
                if (budgetPerPersonPerDay < 20)
                {
                    ModelState.AddModelError(nameof(TravelRequest.Budget),
                        $"Your budget of ${budgetPerPersonPerDay:F0} per person per day might be too low for a comfortable trip. Consider increasing your budget.");
                }
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Form validation failed");
                return Page();
            }

            // After validation passes, now safe to null optional fields
            if (string.IsNullOrWhiteSpace(TravelRequest.DepartureLocation))
                TravelRequest.DepartureLocation = null;

            // Async generation: enqueue job and redirect to result page
            var planId = Guid.NewGuid().ToString();

            // Get user ID (authenticated or anonymous cookie)
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var anonymousCookieId = Request.Cookies["AnonymousUserId"];

            if (string.IsNullOrEmpty(anonymousCookieId))
            {
                anonymousCookieId = Guid.NewGuid().ToString();
                Response.Cookies.Append("AnonymousUserId", anonymousCookieId, new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax
                });
            }

            try
            {
                // Create persistent status record in database
                await _planStatusService.CreateStatusAsync(
                    planId,
                    TravelRequest.Destination,
                    TravelRequest.NumberOfTravelers,
                    days,
                    userId,
                    anonymousCookieId);

                _cache.Set(PlanGenerationWorker.StateKey(planId), new PlanGenerationState
                {
                    Status = PlanGenerationStatus.Queued,
                    StartedAt = DateTimeOffset.UtcNow
                }, TimeSpan.FromMinutes(40));

                await _queue.EnqueueAsync(new PlanGenerationJob(planId, TravelRequest, userId, anonymousCookieId), HttpContext.RequestAborted);

                // Return JSON with planId instead of redirecting
                // The client-side JavaScript will handle the redirect after completion
                return new JsonResult(new { success = true, planId = planId });
            }
            catch (Exception ex)
            {
                // Log and show a friendly message instead of letting an exception bubble to the global error page
                _logger.LogError(ex, "Failed to enqueue plan generation job for destination {Destination}", TravelRequest.Destination);
                return new JsonResult(new { success = false, error = "Wystąpił błąd podczas tworzenia planu. Spróbuj ponownie później." });
            }
        }
    }
}