using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using project.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Text;
using project.Services;
using project.Services.Background;
using Microsoft.EntityFrameworkCore;

namespace project.Pages.TravelPlanner
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class ResultModel : PageModel
    {
        private readonly ILogger<ResultModel> _logger;
        private readonly IMemoryCache _cache;
        private readonly ITravelService _travelService;
        private readonly IPlanJobQueue _queue;
        private readonly IWebHostEnvironment _env;
        private readonly project.Data.ApplicationDbContext _db;
        private readonly IImageService _imageService;
        private const string SavedPlansKeyPrefix = "savedplans:";
        private const string AnonymousCookieName = "anon_saved_plans_id";

        public ResultModel(ILogger<ResultModel> logger, IMemoryCache cache, ITravelService travelService, IPlanJobQueue queue, IWebHostEnvironment env, project.Data.ApplicationDbContext db, IImageService imageService)
        {
            _logger = logger;
            _cache = cache;
            _travelService = travelService;
            _queue = queue;
            _env = env;
            _db = db;
            _imageService = imageService;
        }

        public string? PlanId { get; private set; }
        public TravelPlan? TravelPlan { get; set; }
        public bool IsSaved { get; set; }
        public bool IsProcessing { get; set; }
        public PlanGenerationState? GenerationState { get; set; }
        public List<ParsedDay> ParsedDays { get; private set; } = new();
        public string? RawItinerary { get; private set; }
        public string VisibleItinerary { get; private set; } = string.Empty;
        public (string? PhotographerName, string? PhotographerUrl, string? Source)? DestinationImageAttribution { get; private set; }

        private static string OriginalKey(string id) => $"plan:orig:{id}";

        public IActionResult OnGet(string id, bool? processing)
        {
            PlanId = id;
            _logger.LogInformation("Result page accessed with id={Id}", id);
            if (string.IsNullOrWhiteSpace(id))
            {
                _logger.LogWarning("No plan id provided");
                TempData["ErrorMessage"] = "No travel plan found. Please create a new plan.";
                return RedirectToPage("Index");
            }

            var requestedProcessing = processing == true;

            // First try cache
            TravelPlan? plan = null;
            if (_cache.TryGetValue(id, out TravelPlan? cachedPlan) && cachedPlan != null)
            {
                plan = cachedPlan;
            }
            // If not in cache, try loading from database
            else
            {
                var userId = User?.Identity?.IsAuthenticated == true
                    ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    : null;

                var anonymousCookieId = string.IsNullOrWhiteSpace(userId) && Request.Cookies.TryGetValue(AnonymousCookieName, out var anonId)
                    ? anonId
                    : null;

                // Try to find by ExternalId first, then by database Id
                TravelPlan? dbPlan = null;
                if (userId != null)
                {
                    dbPlan = _db.TravelPlans.FirstOrDefault(tp => tp.ExternalId == id && tp.UserId == userId);
                    if (dbPlan == null && int.TryParse(id, out var numId))
                    {
                        dbPlan = _db.TravelPlans.FirstOrDefault(tp => tp.Id == numId && tp.UserId == userId);
                    }
                }
                else if (anonymousCookieId != null)
                {
                    dbPlan = _db.TravelPlans.FirstOrDefault(tp => tp.ExternalId == id && tp.AnonymousCookieId == anonymousCookieId);
                    if (dbPlan == null && int.TryParse(id, out var numId))
                    {
                        dbPlan = _db.TravelPlans.FirstOrDefault(tp => tp.Id == numId && tp.AnonymousCookieId == anonymousCookieId);
                    }
                }

                if (dbPlan != null)
                {
                    plan = dbPlan;
                    // Load into cache for subsequent requests
                    var cacheId = dbPlan.ExternalId ?? dbPlan.Id.ToString();
                    _cache.Set(cacheId, plan, TimeSpan.FromMinutes(30));
                    _logger.LogInformation("Loaded plan from database: ExternalId={ExtId}, DbId={DbId}", dbPlan.ExternalId, dbPlan.Id);
                }
            }

            if (plan != null)
            {
                TravelPlan = plan;
                // Load image attribution if destination image present
                if (!string.IsNullOrEmpty(TravelPlan.DestinationImageUrl))
                {
                    try
                    {
                        var normalized = TravelPlan.Destination.Trim().ToLowerInvariant();
                        var img = _db.DestinationImages.FirstOrDefault(d => d.Destination == normalized);
                        if (img != null)
                        {
                            DestinationImageAttribution = (img.PhotographerName, img.PhotographerUrl, img.Source);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set attribution for {Destination}", TravelPlan.Destination);
                    }
                }
                // If a persisted version exists on disk, prefer its generated itinerary so changes survive restarts
                try
                {
                    var dir = Path.Combine(_env.ContentRootPath, "Data", "savedPlans");
                    var file = Path.Combine(dir, $"{id}.json");
                    if (global::System.IO.File.Exists(file))
                    {
                        var disk = global::System.IO.File.ReadAllText(file, Encoding.UTF8);
                        try
                        {
                            using var doc = JsonDocument.Parse(disk);
                            if (doc.RootElement.TryGetProperty("generatedItinerary", out var gi) && gi.ValueKind == JsonValueKind.String)
                            {
                                var diskIt = gi.GetString();
                                if (!string.IsNullOrWhiteSpace(diskIt))
                                {
                                    plan.GeneratedItinerary = diskIt;
                                    _cache.Set(id, plan, TimeSpan.FromMinutes(30));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse saved plan JSON for id={Id}", id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load saved plan from disk for id={Id}", id);
                }
                var savedIdsKey = GetSavedPlansKey();
                var savedIds = _cache.GetOrCreate(savedIdsKey, entry => new List<string>());
                savedIds ??= new List<string>();
                IsSaved = savedIds.Contains(id);
                _logger.LogInformation("TravelPlan loaded from cache: ID={Id}, Destination={Destination}", plan.Id, plan.Destination);

                // PROCESSING OVERLAY: CHECK STATE
                GenerationState = _cache.Get<PlanGenerationState>($"planstatus:{id}");
                // SHOW OVERLAY IF PROCESSING FLAG SET
                IsProcessing = requestedProcessing;

                // CACHE ORIGINAL ITINERARY
                if (!_cache.TryGetValue(OriginalKey(id), out string? _))
                {
                    var orig = plan.GeneratedItinerary ?? string.Empty;
                    _cache.Set(OriginalKey(id), orig, TimeSpan.FromHours(1));
                }

                ParseItinerary(plan.GeneratedItinerary);
                return Page();
            }

            GenerationState = _cache.Get<PlanGenerationState>($"planstatus:{id}");
            // If plan not in cache, attempt to load a persisted anonymous snapshot from disk
            if (TravelPlan == null && (_cache.TryGetValue(id, out TravelPlan? tmp) == false || tmp == null))
            {
                try
                {
                    var dir = Path.Combine(_env.ContentRootPath, "Data", "savedPlans");
                    var file = Path.Combine(dir, $"{id}.json");
                    if (global::System.IO.File.Exists(file))
                    {
                        var raw = global::System.IO.File.ReadAllText(file, Encoding.UTF8);
                        using var doc = JsonDocument.Parse(raw);
                        var dest = doc.RootElement.TryGetProperty("destination", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() ?? string.Empty : string.Empty;
                        var gen = doc.RootElement.TryGetProperty("generatedItinerary", out var gi) && gi.ValueKind == JsonValueKind.String ? gi.GetString() ?? string.Empty : string.Empty;
                        var start = DateTime.TryParse(doc.RootElement.GetProperty("startDate").GetString(), out var sd) ? sd : DateTime.Today;
                        var end = DateTime.TryParse(doc.RootElement.GetProperty("endDate").GetString(), out var ed) ? ed : DateTime.Today;
                        var planFromDisk = new TravelPlan
                        {
                            Destination = dest,
                            GeneratedItinerary = gen,
                            StartDate = start,
                            EndDate = end,
                        };
                        TravelPlan = planFromDisk;
                        _cache.Set(id, planFromDisk, TimeSpan.FromMinutes(30));

                        // mark saved state if found in anon index
                        var savedIdsKey = GetSavedPlansKey();
                        var savedIds = _cache.GetOrCreate(savedIdsKey, entry => new List<string>());
                        if ((savedIds == null || savedIds.Count == 0) && savedIdsKey.StartsWith(SavedPlansKeyPrefix + "anon:"))
                        {
                            try
                            {
                                var anonId = savedIdsKey.Substring((SavedPlansKeyPrefix + "anon:").Length);
                                var indexFile = Path.Combine(dir, $"anon-{anonId}.json");
                                if (global::System.IO.File.Exists(indexFile))
                                {
                                    var rawIndex = global::System.IO.File.ReadAllText(indexFile, Encoding.UTF8);
                                    var diskIds = JsonSerializer.Deserialize<List<string>>(rawIndex) ?? new List<string>();
                                    savedIds = diskIds;
                                    _cache.Set(savedIdsKey, savedIds, TimeSpan.FromDays(7));
                                }
                            }
                            catch { }
                        }
                        IsSaved = (savedIds ?? new List<string>()).Contains(id);

                        ParseItinerary(TravelPlan.GeneratedItinerary);
                        // Load attribution also for disk/anon path
                        if (!string.IsNullOrEmpty(TravelPlan.DestinationImageUrl))
                        {
                            try
                            {
                                var normalized2 = TravelPlan.Destination.Trim().ToLowerInvariant();
                                var img2 = _db.DestinationImages.FirstOrDefault(d => d.Destination == normalized2);
                                if (img2 != null)
                                {
                                    DestinationImageAttribution = (img2.PhotographerName, img2.PhotographerUrl, img2.Source);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to set attribution (disk path) for {Destination}", TravelPlan.Destination);
                            }
                        }
                        return Page();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load persisted plan for id={Id}", id);
                }
            }
            if (GenerationState != null && (GenerationState.Status == PlanGenerationStatus.Queued || GenerationState.Status == PlanGenerationStatus.InProgress))
            {
                IsProcessing = true;
                return Page();
            }

            if (GenerationState != null && GenerationState.Status == PlanGenerationStatus.Failed)
            {
                TempData["ErrorMessage"] = GenerationState.Error ?? "Failed to generate the plan.";
                return RedirectToPage("Index");
            }

            _logger.LogWarning("TravelPlan not found in cache for id: {Id}", id);
            TempData["ErrorMessage"] = "No travel plan found. Please create a new plan.";
            return RedirectToPage("Index");
        }

        /// <summary>
        /// Provides image attribution details for Unsplash image if cached
        /// </summary>
        public async Task<(string? PhotographerName, string? PhotographerUrl, string Source)?> GetImageAttributionAsync()
        {
            if (TravelPlan == null || string.IsNullOrEmpty(TravelPlan.DestinationImageUrl)) return null;

            try
            {
                // Normalize destination to match cache key
                var normalized = TravelPlan.Destination.Trim().ToLowerInvariant();
                var img = await _db.DestinationImages
                    .Where(d => d.Destination == normalized)
                    .FirstOrDefaultAsync();
                if (img == null) return null;
                return (img.PhotographerName, img.PhotographerUrl, img.Source);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load image attribution for {Destination}", TravelPlan.Destination);
                return null;
            }
        }

        private string GetSavedPlansKey()
        {
            // If user is authenticated, scope saved plans to user id
            if (User?.Identity?.IsAuthenticated == true)
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(userId)) return SavedPlansKeyPrefix + "user:" + userId;
            }

            // Otherwise use an anonymous cookie identifier stored for 1 year
            var anon = GetOrCreateAnonymousId();
            return SavedPlansKeyPrefix + "anon:" + anon;
        }

        private string GetOrCreateAnonymousId()
        {
            if (Request.Cookies.TryGetValue(AnonymousCookieName, out var existing) && !string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            var newId = Guid.NewGuid().ToString();
            var cookieOptions = new Microsoft.AspNetCore.Http.CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = true,
                IsEssential = true,
                Secure = Request.IsHttps
            };
            Response.Cookies.Append(AnonymousCookieName, newId, cookieOptions);
            return newId;
        }

        public IActionResult OnPostUpdateItinerary(string id, string updatedItinerary)
        {
            if (string.IsNullOrWhiteSpace(id)) { TempData["ErrorMessage"] = "No travel plan found to update."; return RedirectToPage("Index"); }
            if (!_cache.TryGetValue(id, out TravelPlan? plan) || plan == null) { TempData["ErrorMessage"] = "No travel plan found to update."; return RedirectToPage("Index"); }
            plan.GeneratedItinerary = (updatedItinerary ?? string.Empty).Trim();
            _cache.Set(id, plan, TimeSpan.FromMinutes(30));
            TempData["SuccessMessage"] = "Itinerary updated successfully.";
            return RedirectToPage("Result", new { id });
        }

        public IActionResult OnPostResetItinerary(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) { TempData["ErrorMessage"] = "No travel plan found to reset."; return RedirectToPage("Index"); }
            if (!_cache.TryGetValue(id, out TravelPlan? plan) || plan == null) { TempData["ErrorMessage"] = "No travel plan found to reset."; return RedirectToPage("Index"); }
            if (!_cache.TryGetValue(OriginalKey(id), out string? original) || original == null)
            {
                TempData["ErrorMessage"] = "Original itinerary is not available.";
                return RedirectToPage("Result", new { id });
            }

            plan.GeneratedItinerary = original;
            _cache.Set(id, plan, TimeSpan.FromMinutes(30));
            TempData["SuccessMessage"] = "Itinerary reset to original.";
            return RedirectToPage("Result", new { id });
        }

        private void ParseItinerary(string? raw)
        {
            RawItinerary = null;
            ParsedDays.Clear();
            VisibleItinerary = string.Empty;
            if (string.IsNullOrWhiteSpace(raw)) { RawItinerary = string.Empty; VisibleItinerary = string.Empty; return; }

            string text = raw.Trim();
            try
            {
                if (text.StartsWith("{"))
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("itinerary", out var it) && it.ValueKind == JsonValueKind.String)
                    {
                        text = it.GetString() ?? text;
                    }
                }
            }
            catch { }

            var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            // ACCEPT COMMON DAY HEADER VARIANTS (ENGLISH + POLISH)
            // Matches: "Day 1", "1.", "Dzień 1", "Dzień 1 - Oct 20" etc.
            var regex = new System.Text.RegularExpressions.Regex(@"^\s*(?:\d+\.|[Dd]ay|[Dd]zie[nń])\s*(?<n>\d+)\s*(?:[:\-\)\(]+\s*(?<date>.*))?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            int currentDay = 0; string currentDate = string.Empty; List<string>? buf = null; bool started = false;
            foreach (var l in lines)
            {
                var t = l.Trim();
                if (string.IsNullOrEmpty(t)) { if (started && buf != null) buf.Add(string.Empty); continue; }
                var m = regex.Match(t);
                if (m.Success)
                {
                    started = true;
                    if (buf != null) { ParsedDays.Add(new ParsedDay(currentDay == 0 ? ParsedDays.Count + 1 : currentDay, currentDate, buf.ToArray())); }
                    currentDay = int.TryParse(m.Groups["n"].Value, out var n) ? n : (ParsedDays.Count + 1);
                    currentDate = m.Groups["date"].Value.Trim();
                    buf = new List<string>();
                }
                else
                {
                    if (!started) continue;
                    (buf ??= new List<string>()).Add(t);
                }
            }
            if (buf != null) { ParsedDays.Add(new ParsedDay(currentDay == 0 ? ParsedDays.Count + 1 : currentDay, currentDate, buf.ToArray())); }

            if (ParsedDays.Count == 0)
            {
                RawItinerary = text;
                VisibleItinerary = text;
            }
            else
            {
                var sb = new StringBuilder();
                foreach (var d in ParsedDays)
                {
                    var header = $"Day {d.Day}" + (string.IsNullOrWhiteSpace(d.Date) ? string.Empty : $" - {d.Date}");
                    sb.AppendLine(header);
                    foreach (var line in d.Lines) sb.AppendLine(line);
                    sb.AppendLine();
                }
                VisibleItinerary = sb.ToString().TrimEnd();
            }
        }

        public IActionResult OnGetStatus(string id)
        {
            var state = _cache.Get<PlanGenerationState>($"planstatus:{id}");
            var hasPlan = _cache.TryGetValue(id, out TravelPlan? _);
            var status = state?.Status ?? (hasPlan ? PlanGenerationStatus.Completed : PlanGenerationStatus.Failed);
            return new JsonResult(new { status = status.ToString(), error = state?.Error });
        }

        public IActionResult OnPostSave(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) { TempData["ErrorMessage"] = "No travel plan found to save."; return RedirectToPage("Index"); }
            if (!_cache.TryGetValue(id, out TravelPlan? plan) || plan == null) { TempData["ErrorMessage"] = "No travel plan found to save."; return RedirectToPage("Index"); }

            // Always persist to DB (for both authenticated and anonymous users)
            var userId = User?.Identity?.IsAuthenticated == true
                ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                : null;

            var anonymousCookieId = string.IsNullOrWhiteSpace(userId)
                ? GetOrCreateAnonymousId()
                : null;

            // Check if already exists (by ExternalId and UserId/AnonymousCookieId)
            var existing = userId != null
                ? _db.TravelPlans.FirstOrDefault(tp => tp.ExternalId == id && tp.UserId == userId)
                : _db.TravelPlans.FirstOrDefault(tp => tp.ExternalId == id && tp.AnonymousCookieId == anonymousCookieId);

            if (existing == null)
            {
                var toSave = new TravelPlan
                {
                    Destination = plan.Destination,
                    StartDate = plan.StartDate,
                    EndDate = plan.EndDate,
                    NumberOfTravelers = plan.NumberOfTravelers,
                    Budget = plan.Budget,
                    TravelPreferences = plan.TravelPreferences ?? string.Empty,
                    GeneratedItinerary = plan.GeneratedItinerary ?? string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    UserId = userId,
                    AnonymousCookieId = anonymousCookieId,
                    ExternalId = id,
                    Accommodations = plan.Accommodations,
                    Activities = plan.Activities,
                    Transportation = plan.Transportation,
                    DestinationImageUrl = plan.DestinationImageUrl
                };
                _db.TravelPlans.Add(toSave);
                _db.SaveChanges();
                _logger.LogInformation("Plan saved to DB: id={Id}, userId={UserId}, anonId={AnonId}, imageUrl={ImageUrl}", id, userId ?? "null", anonymousCookieId ?? "null", plan.DestinationImageUrl ?? "null");
            }
            else
            {
                // Plan already exists (auto-saved by worker) - just update if needed
                if (existing.GeneratedItinerary != plan.GeneratedItinerary || existing.DestinationImageUrl != plan.DestinationImageUrl)
                {
                    existing.GeneratedItinerary = plan.GeneratedItinerary ?? string.Empty;
                    existing.DestinationImageUrl = plan.DestinationImageUrl;
                    _db.SaveChanges();
                    _logger.LogInformation("Plan updated in DB: id={Id}", id);
                }
                else
                {
                    _logger.LogInformation("Plan already saved and up-to-date: id={Id}", id);
                }
            }

            // Add to saved plans list in cache
            var savedIdsKey = GetSavedPlansKey();
            var savedIds = _cache.GetOrCreate(savedIdsKey, entry =>
            {
                entry.SetSlidingExpiration(TimeSpan.FromDays(365));
                return new List<string>();
            });
            savedIds ??= new List<string>();
            if (!savedIds.Contains(id))
            {
                savedIds.Add(id);
                _cache.Set(savedIdsKey, savedIds, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromDays(365)
                });
                _logger.LogInformation("Added plan {Id} to saved list cache key {Key}", id, savedIdsKey);
            }

            TempData["SuccessMessage"] = "Plan saved.";
            return RedirectToPage("Result", new { id });
        }

        public IActionResult OnPostRemove(string id)
        {
            // Remove from DB (for both authenticated and anonymous users)
            var userId = User?.Identity?.IsAuthenticated == true
                ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                : null;

            var anonymousCookieId = string.IsNullOrWhiteSpace(userId)
                ? GetOrCreateAnonymousId()
                : null;

            var existing = userId != null
                ? _db.TravelPlans.FirstOrDefault(tp => tp.ExternalId == id && tp.UserId == userId)
                : _db.TravelPlans.FirstOrDefault(tp => tp.ExternalId == id && tp.AnonymousCookieId == anonymousCookieId);

            if (existing != null)
            {
                _db.TravelPlans.Remove(existing);
                _db.SaveChanges();
                _logger.LogInformation("Plan removed from DB: id={Id}", id);
            }

            // Remove from saved plans list in cache
            var savedIdsKey = GetSavedPlansKey();
            var savedIds = _cache.GetOrCreate(savedIdsKey, entry =>
            {
                entry.SetSlidingExpiration(TimeSpan.FromDays(365));
                return new List<string>();
            });
            savedIds ??= new List<string>();
            if (savedIds.Contains(id))
            {
                savedIds.Remove(id);
                _cache.Set(savedIdsKey, savedIds, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromDays(365)
                });
                _logger.LogInformation("Removed plan {Id} from saved list cache key {Key}", id, savedIdsKey);
            }

            TempData["SuccessMessage"] = "Plan removed from saved.";
            return RedirectToPage("Result", new { id });
        }

        public async Task<IActionResult> OnPostUpdateDetails(string id, string? startDate, string? endDate, int? numberOfTravelers, decimal? budget, string? travelPreferences, string? accommodations, string? activities, string? transportation)
        {
            if (string.IsNullOrWhiteSpace(id)) { TempData["ErrorMessage"] = "No travel plan found to update."; return RedirectToPage("Index"); }
            if (!_cache.TryGetValue(id, out TravelPlan? plan) || plan == null) { TempData["ErrorMessage"] = "No travel plan found to update."; return RedirectToPage("Index"); }
            // PARSE INCOMING DATES
            DateTime parsedStart = plan.StartDate;
            DateTime parsedEnd = plan.EndDate;
            if (!string.IsNullOrWhiteSpace(startDate) && DateTime.TryParse(startDate, out var sd)) parsedStart = sd;
            if (!string.IsNullOrWhiteSpace(endDate) && DateTime.TryParse(endDate, out var ed)) parsedEnd = ed;

            int parsedTravelers = numberOfTravelers ?? plan.NumberOfTravelers;
            decimal parsedBudget = budget ?? plan.Budget;

            var incomingPreferences = travelPreferences ?? string.Empty;

            // Basic validation
            if (parsedEnd <= parsedStart)
            {
                TempData["ErrorMessage"] = "End date must be after start date.";
                return RedirectToPage("Result", new { id });
            }
            if (parsedTravelers < 1 || parsedTravelers > 100)
            {
                TempData["ErrorMessage"] = "Number of travelers must be between 1 and 100.";
                return RedirectToPage("Result", new { id });
            }
            if (parsedBudget < 0)
            {
                TempData["ErrorMessage"] = "Budget must be zero or more.";
                return RedirectToPage("Result", new { id });
            }

            bool shouldRegenerate = parsedStart != plan.StartDate || parsedEnd != plan.EndDate || parsedTravelers != plan.NumberOfTravelers || parsedBudget != plan.Budget || incomingPreferences != (plan.TravelPreferences ?? string.Empty);

            // Update simple lists
            var newAccom = (accommodations ?? string.Empty).Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            var newActs = (activities ?? string.Empty).Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            var newTrans = (transportation ?? string.Empty).Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

            if (shouldRegenerate)
            {
                // Enqueue regeneration job and set state to queued. The background worker will pick it up.
                var req = new TravelPlanRequest
                {
                    Destination = plan.Destination,
                    StartDate = parsedStart,
                    EndDate = parsedEnd,
                    NumberOfTravelers = parsedTravelers,
                    Budget = parsedBudget,
                    TravelPreferences = incomingPreferences,
                    TripType = null
                };

                // update cache state to queued
                _cache.Set(PlanGenerationWorker.StateKey(id), new PlanGenerationState
                {
                    Status = PlanGenerationStatus.Queued,
                    StartedAt = DateTimeOffset.UtcNow
                }, TimeSpan.FromMinutes(40));

                // Get user/anon ID for regeneration
                var userId = User?.Identity?.IsAuthenticated == true
                    ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    : null;
                var anonymousCookieId = GetOrCreateAnonymousId();

                // Enqueue job (fire-and-forget)
                _ = _queue.EnqueueAsync(new PlanGenerationJob(id, req, userId, anonymousCookieId));

                TempData["SuccessMessage"] = "Regeneration enqueued. Please wait a moment while the plan is regenerated.";
                return RedirectToPage("Result", new { id, processing = true });
            }

            // If not regenerating (or regeneration failed), persist lists and preferences only
            plan.StartDate = parsedStart;
            plan.EndDate = parsedEnd;
            plan.NumberOfTravelers = parsedTravelers;
            plan.Budget = parsedBudget;
            plan.TravelPreferences = incomingPreferences;
            plan.Accommodations = newAccom;
            plan.Activities = newActs;
            plan.Transportation = newTrans;
            _cache.Set(id, plan, TimeSpan.FromMinutes(30));
            ParseItinerary(plan.GeneratedItinerary);
            TempData["SuccessMessage"] = shouldRegenerate ? "Plan regenerated." : "Details updated.";
            return RedirectToPage("Result", new { id });
        }

        // Persist reordered days/lines sent from client as JSON payload
        private record DayOrder(int day, string? date, List<string>? lines);

        public async Task<IActionResult> OnPostSaveOrder(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest(new { error = "No plan id provided" });
            if (!_cache.TryGetValue(id, out TravelPlan? plan) || plan == null) return NotFound(new { error = "Plan not found" });

            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }
            if (string.IsNullOrWhiteSpace(body)) return BadRequest(new { error = "Empty payload" });

            List<DayOrder>? days = null;
            try
            {
                days = JsonSerializer.Deserialize<List<DayOrder>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return BadRequest(new { error = "Invalid JSON payload" });
            }

            if (days == null || days.Count == 0)
            {
                return BadRequest(new { error = "No days provided" });
            }

            // Build a simple textual itinerary from the provided days
            var sb = new StringBuilder();
            foreach (var d in days.OrderBy(d => d.day))
            {
                var header = $"Day {d.day}" + (string.IsNullOrWhiteSpace(d.date) ? string.Empty : $" - {d.date}");
                sb.AppendLine(header);
                if (d.lines != null)
                {
                    foreach (var l in d.lines)
                    {
                        if (!string.IsNullOrWhiteSpace(l)) sb.AppendLine(l);
                    }
                }
                sb.AppendLine();
            }

            plan.GeneratedItinerary = sb.ToString().TrimEnd();
            _cache.Set(id, plan, TimeSpan.FromMinutes(30));

            // Persist to disk for durability (quick JSON snapshot)
            try
            {
                var dir = Path.Combine(_env.ContentRootPath, "Data", "savedPlans");
                if (!global::System.IO.Directory.Exists(dir)) global::System.IO.Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, $"{id}.json");
                var snapshot = new { id = id, generatedItinerary = plan.GeneratedItinerary, savedAt = DateTimeOffset.UtcNow };
                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                global::System.IO.File.WriteAllText(file, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist plan to disk for id={Id}", id);
            }

            return new JsonResult(new { ok = true, message = "Day order saved successfully." });
        }

        // (seed helper removed for security)
    }
}