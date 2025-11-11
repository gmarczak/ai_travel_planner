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

        public void OnGet()
        {
            // SET DEFAULT VALUES
            TravelRequest.StartDate = DateTime.Today.AddDays(14);
            TravelRequest.EndDate = DateTime.Today.AddDays(21);
            TravelRequest.NumberOfTravelers = 2;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation("Processing travel plan request for destination: {Destination}", TravelRequest.Destination);

            // NORMALIZE OPTIONAL FIELDS
            if (string.IsNullOrWhiteSpace(TravelRequest.TravelPreferences))
                TravelRequest.TravelPreferences = null;
            if (string.IsNullOrWhiteSpace(TravelRequest.TripType))
                TravelRequest.TripType = null;

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

            // BUDGET VALIDATION
            var days = (TravelRequest.EndDate - TravelRequest.StartDate).Days + 1;
            var budgetPerPersonPerDay = TravelRequest.Budget / TravelRequest.NumberOfTravelers / days;

            if (budgetPerPersonPerDay < 20)
            {
                ModelState.AddModelError(nameof(TravelRequest.Budget),
                    $"Your budget of ${budgetPerPersonPerDay:F0} per person per day might be too low for a comfortable trip. Consider increasing your budget.");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Form validation failed");
                return Page();
            }

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

        public async Task<IActionResult> OnGetDestinationSuggestionsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return new JsonResult(new List<TravelSuggestion>());
            }

            try
            {
                var suggestions = await _travelService.GetDestinationSuggestionsAsync(query);
                return new JsonResult(suggestions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting destination suggestions for: {Query}", query);
                return new JsonResult(new List<TravelSuggestion>());
            }
        }
    }
}