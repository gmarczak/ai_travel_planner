using project.Services;
using project.Data;
using project.Models;
using DotNetEnv;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using project.Services.Background;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// LOAD ENVIRONMENT VARIABLES
try
{
    var envPath = Path.Combine(builder.Environment.ContentRootPath, ".env");
    if (File.Exists(envPath)) Env.Load(envPath);
    else Env.Load();
}
catch { }

// ADD ENVIRONMENT VARIABLES TO CONFIG
builder.Configuration.AddEnvironmentVariables();

// LOGGING FILTERS: suppress EF Core SQL command logs
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

// NORMALIZE GOOGLE MAPS API KEY
var mapsApiKey = builder.Configuration["GoogleMaps:ApiKey"]
                 ?? builder.Configuration["GoogleMaps__ApiKey"]
                 ?? builder.Configuration["GOOGLE_MAPS_API_KEY"]
                 ?? builder.Configuration["GOOGLEMAPS_API_KEY"]
                 ?? builder.Configuration["GOOGLE_MAPS_KEY"];
if (!string.IsNullOrWhiteSpace(mapsApiKey))
{
    builder.Configuration["GoogleMaps:ApiKey"] = mapsApiKey;
}

// ADD DATABASE CONTEXT
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=travelplanner.db";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// ADD IDENTITY
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;

    // User settings
    options.User.RequireUniqueEmail = true;

    // Sign in settings
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

// ADD MEMORY CACHE
builder.Services.AddMemoryCache();

// BACKGROUND PLAN GENERATION QUEUE + WORKER
builder.Services.AddSingleton<IPlanJobQueue, PlanGenerationQueue>();
builder.Services.AddHostedService<PlanGenerationWorker>();

// PROVIDER SELECTION (OPENAI OR CLAUDE)
var provider = builder.Configuration["AI:Provider"] ?? "OpenAI";
var openAiApiKey = builder.Configuration["OPENAI_API_KEY"] ?? builder.Configuration["OpenAI:ApiKey"];
var claudeApiKey = builder.Configuration["ANTHROPIC_API_KEY"] ?? builder.Configuration["Anthropic:ApiKey"] ?? builder.Configuration["CLAUDE_API_KEY"];
var useOpenAI = provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(openAiApiKey);
var useClaude = provider.Equals("Claude", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(claudeApiKey);

// RAZOR PAGES
builder.Services.AddRazorPages();

// ERROR MONITORING
builder.Services.AddScoped<IErrorMonitoringService, ErrorMonitoringService>();

// Saved plans service (merging anonymous plans into user account)
builder.Services.AddScoped<SavedPlansService>();

// REGISTER TRAVEL SERVICE
if (useClaude)
{
    Console.WriteLine("+ Using Claude (Anthropic) Travel Service");
    builder.Services.AddScoped<ITravelService, ClaudeTravelService>();
}
else if (useOpenAI)
{
    Console.WriteLine("+ Using OpenAI Travel Service");
    builder.Services.AddScoped<ITravelService, OpenAITravelService>();

    // ADD OPENAI API KEY TO CONFIG
    if (!string.IsNullOrWhiteSpace(builder.Configuration["OPENAI_API_KEY"]))
    {
        builder.Configuration["OpenAI:ApiKey"] = builder.Configuration["OPENAI_API_KEY"];
    }
}
else
{
    Console.WriteLine("* Using Mock Travel Service (no provider or API key)");
    builder.Services.AddScoped<ITravelService, TravelService>();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Small endpoint to expose client-side config such as Google Maps API key
// This returns only non-sensitive configuration values intended for client use.
app.MapGet("/__config/maps", (IConfiguration config) =>
{
    var key = config["GoogleMaps:ApiKey"] ?? string.Empty;
    return Results.Json(new { googleMapsApiKey = key });
});

// STARTUP INFO
Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║          🚀 AI TRAVEL PLANNER STARTED                      ║");
Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║ Time:       {DateTime.Now:yyyy-MM-dd HH:mm:ss}                         ║");
Console.WriteLine($"║ Environment: {app.Environment.EnvironmentName,-43} ║");

if (useClaude)
{
    Console.WriteLine("║ AI Provider: Claude (Anthropic) ✅                         ║");
    Console.WriteLine("║ API Status:  Configured                                    ║");
}
else if (useOpenAI)
{
    Console.WriteLine("║ AI Provider: OpenAI ✅                                     ║");
    Console.WriteLine("║ API Status:  Configured                                    ║");
}
else
{
    Console.WriteLine("║ AI Provider: DEMO MODE (Mock) ⚠️                          ║");
    Console.WriteLine("║ API Status:  Not configured                                ║");
}

Console.WriteLine("║ Auth System: ASP.NET Core Identity ✅                      ║");
Console.WriteLine("║ Database:    SQLite (travelplanner.db) ✅                  ║");
Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
Console.WriteLine("║ URL:         http://localhost:5000                         ║");
Console.WriteLine("║ Login:       http://localhost:5000/Account/Login          ║");
Console.WriteLine("║ Register:    http://localhost:5000/Account/Register       ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
Console.WriteLine();

if (!useOpenAI && !useClaude)
{
    Console.WriteLine("⚠️  WARNING: AI provider not configured!");
    Console.WriteLine("   To enable AI features:");
    Console.WriteLine("   - For Claude: set ANTHROPIC_API_KEY");
    Console.WriteLine("   - For OpenAI: set OPENAI_API_KEY");
    Console.WriteLine();
}

app.Run();