using project.Services;
using project.Data;
using project.Models;
using DotNetEnv;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using project.Services.Background;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════
// SECURE KEY STORAGE CONFIGURATION
// ═══════════════════════════════════════════════════════════
// Priority order:
// 1. Azure Key Vault (Production)
// 2. User Secrets (Development - most secure for dev)
// 3. Environment Variables from .env (Fallback)
// 4. appsettings.json (Last resort - placeholder values only)
// ═══════════════════════════════════════════════════════════

if (builder.Environment.IsProduction())
{
    // PRODUCTION: Use Azure Key Vault
    var keyVaultName = builder.Configuration["KeyVault:Name"];
    if (!string.IsNullOrEmpty(keyVaultName) && keyVaultName != "your-keyvault-name")
    {
        var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");

        // Use DefaultAzureCredential which tries multiple auth methods:
        // - Environment variables (for Azure deployment)
        // - Managed Identity (for Azure App Service/Functions)
        // - Azure CLI (for local development)
        builder.Configuration.AddAzureKeyVault(
            keyVaultUri,
            new DefaultAzureCredential()
        );

        Console.WriteLine($"[INFO] Loaded secrets from Azure Key Vault: {keyVaultName}");
    }
    else
    {
        Console.WriteLine("[WARN] KeyVault:Name not configured. Add to appsettings.Production.json");
    }
}
else if (builder.Environment.IsDevelopment())
{
    // DEVELOPMENT: Use User Secrets (secure, not in source control)
    // Secrets are stored in: %APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json
    // To set secrets, use: dotnet user-secrets set "OpenAI:ApiKey" "your-key-here"
    Console.WriteLine("[INFO] Development mode: Using User Secrets for API keys");
    Console.WriteLine("[INFO] Set secrets with: dotnet user-secrets set \"OpenAI:ApiKey\" \"your-key\"");
}

// FALLBACK: Load .env file (backward compatibility)
try
{
    var envPath = Path.Combine(builder.Environment.ContentRootPath, ".env");
    if (File.Exists(envPath))
    {
        Env.Load(envPath);
        Console.WriteLine("[INFO] Loaded environment variables from .env file");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[WARN] Failed to load .env file: {ex.Message}");
}

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
// Use SQL Server in production, SQLite in development
if (builder.Environment.IsProduction())
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Production requires DefaultConnection connection string for Azure SQL Database");
    }
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString, sql =>
        {
            // Enable resilient SQL connections in Azure
            sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
            sql.CommandTimeout(120);
        }));
    Console.WriteLine("[INFO] Using SQL Server (Azure SQL Database)");
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=travelplanner.db";
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
    Console.WriteLine($"[INFO] Using SQLite: {connectionString}");
}

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
builder.Services.AddHostedService<CacheCleanupWorker>();

// PROVIDER SELECTION (OPENAI OR CLAUDE)
var provider = builder.Configuration["AI:Provider"] ?? "OpenAI";
var openAiApiKey = builder.Configuration["OPENAI_API_KEY"] ?? builder.Configuration["OpenAI:ApiKey"];
var claudeApiKey = builder.Configuration["ANTHROPIC_API_KEY"] ?? builder.Configuration["Anthropic:ApiKey"] ?? builder.Configuration["CLAUDE_API_KEY"];
var useOpenAI = provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(openAiApiKey);
var useClaude = provider.Equals("Claude", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(claudeApiKey);

// SIGNALR
builder.Services.AddSignalR();

// RAZOR PAGES
builder.Services.AddRazorPages();
// Ensure localization services are available to satisfy any leftover view injections
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// ERROR MONITORING
builder.Services.AddScoped<IErrorMonitoringService, ErrorMonitoringService>();

// AI CACHE SERVICE
builder.Services.AddScoped<IAiCacheService, AiCacheService>();

// PLAN STATUS SERVICE (for persistent status tracking with SignalR)
builder.Services.AddScoped<IPlanStatusService, PlanStatusService>();

// Saved plans service (merging anonymous plans into user account)
builder.Services.AddScoped<SavedPlansService>();

// ADD HTTP CLIENT FACTORY (for OpenRouter and other HTTP-based services)
builder.Services.AddHttpClient();

// Register Image Service with HttpClient for Unsplash API
builder.Services.AddHttpClient<IImageService, UnsplashImageService>();

// REGISTER AI SERVICES (with fallback support)
var enableFallback = builder.Configuration.GetValue<bool>("AI:EnableFallback", true);

// Register individual AI providers as IAiService
builder.Services.AddScoped<OpenAITravelService>();
builder.Services.AddScoped<IAiService>(sp => sp.GetRequiredService<OpenAITravelService>());

// Register OpenRouter as additional fallback
builder.Services.AddScoped<OpenRouterAiService>();

// REGISTER TRAVEL SERVICE with fallback logic
if (enableFallback && (useOpenAI || useClaude))
{
    Console.WriteLine("[INFO] Using AI Service with Fallback Support");
    Console.WriteLine($"[INFO] Primary: {(useOpenAI ? "OpenAI" : "Claude")}");
    Console.WriteLine("[INFO] Fallback: OpenRouter");

    // Register primary service
    if (useClaude)
    {
        builder.Services.AddScoped<ITravelService, ClaudeTravelService>();
    }
    else if (useOpenAI)
    {
        builder.Services.AddScoped<ITravelService>(sp =>
        {
            var aiServices = new List<IAiService>
            {
                sp.GetRequiredService<OpenAITravelService>(),
                sp.GetRequiredService<OpenRouterAiService>()
            };
            return new FallbackAiService(aiServices, sp.GetRequiredService<ILogger<FallbackAiService>>());
        });

        // ADD OPENAI API KEY TO CONFIG
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OPENAI_API_KEY"]))
        {
            builder.Configuration["OpenAI:ApiKey"] = builder.Configuration["OPENAI_API_KEY"];
        }
    }
}
else if (useClaude)
{
    Console.WriteLine("[INFO] Using Claude (Anthropic) Travel Service");
    builder.Services.AddScoped<ITravelService, ClaudeTravelService>();
}
else if (useOpenAI)
{
    Console.WriteLine("[INFO] Using OpenAI Travel Service");
    builder.Services.AddScoped<ITravelService, OpenAITravelService>();

    // ADD OPENAI API KEY TO CONFIG
    if (!string.IsNullOrWhiteSpace(builder.Configuration["OPENAI_API_KEY"]))
    {
        builder.Configuration["OpenAI:ApiKey"] = builder.Configuration["OPENAI_API_KEY"];
    }
}
else
{
    Console.WriteLine("[INFO] Using Mock Travel Service (no provider or API key)");
    builder.Services.AddScoped<ITravelService, TravelService>();
}

var app = builder.Build();

// Configure request localization
// (Localization removed) - app uses default request culture

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

// MAP SIGNALR HUBS
app.MapHub<project.Hubs.PlanGenerationHub>("/hubs/planGeneration");

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
Console.WriteLine("============================================================");
Console.WriteLine("           AI TRAVEL PLANNER STARTED                        ");
Console.WriteLine("============================================================");
Console.WriteLine($"Time:        {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");

if (useClaude)
{
    Console.WriteLine("AI Provider: Claude (Anthropic) [CONFIGURED]");
    Console.WriteLine("API Status:  Ready");
}
else if (useOpenAI)
{
    Console.WriteLine("AI Provider: OpenAI [CONFIGURED]");
    Console.WriteLine("API Status:  Ready");
}
else
{
    Console.WriteLine("AI Provider: DEMO MODE (Mock) [NOT CONFIGURED]");
    Console.WriteLine("API Status:  Not configured");
}

Console.WriteLine("Auth System: ASP.NET Core Identity");
if (app.Environment.IsProduction())
{
    Console.WriteLine("Database:    Azure SQL Database");
}
else
{
    Console.WriteLine("Database:    SQLite (Development)");
}
Console.WriteLine("------------------------------------------------------------");
Console.WriteLine("URL:         http://localhost:5000");
Console.WriteLine("Login:       http://localhost:5000/Account/Login");
Console.WriteLine("Register:    http://localhost:5000/Account/Register");
Console.WriteLine("============================================================");
Console.WriteLine();

// Ensure the database schema exists on startup. On Azure the DB file may exist
// but not have tables applied. Try applying EF migrations if present, and
// fall back to EnsureCreated() when migrations are not available.
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            // If there are migrations in the assembly, apply them. If there are no
            // migrations (common for simple SQLite deployments), use EnsureCreated()
            // to create the schema.
            // In Development mode (SQLite), use EnsureCreated() to avoid migration issues
            // In Production mode (Azure SQL), use Migrate() for proper schema versioning
            if (app.Environment.IsDevelopment())
            {
                Console.WriteLine("[DATABASE] Creating SQLite database schema...");
                db.Database.EnsureCreated();
                Console.WriteLine("[DATABASE] SQLite database created successfully.");
            }
            else
            {
                var availableMigrations = db.Database.GetMigrations();
                if (availableMigrations != null && availableMigrations.Any())
                {
                    Console.WriteLine("[MIGRATION] Applying EF Core migrations...");
                    // Use explicit retry for migration step (not covered by execution strategy automatically pre-connection)
                    var attempt = 0;
                    const int maxAttempts = 5;
                    while (true)
                    {
                        try
                        {
                            db.Database.Migrate();
                            Console.WriteLine("[MIGRATION] Migrations applied successfully.");
                            break;
                        }
                        catch (SqlException sx) when (attempt < maxAttempts)
                        {
                            attempt++;
                            var delay = TimeSpan.FromSeconds(2 * attempt);
                            Console.WriteLine($"[WARN] Migration transient failure (attempt {attempt}/{maxAttempts}): {sx.Message}. Retrying in {delay.TotalSeconds}s...");
                            Thread.Sleep(delay);
                            continue;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("[MIGRATION] No migrations found. Ensuring database is created...");
                    db.Database.EnsureCreated();
                    Console.WriteLine("[MIGRATION] Database created successfully.");
                }
            }
        }
        catch (Exception migEx)
        {
            // Catch-all for migration/ensure errors
            Console.WriteLine($"[ERROR] Migration/Ensure step failed: {migEx.Message}");
        }
    }
}
catch (Exception ex)
{
    // Log to console - avoid throwing so startup can continue and the app can surface
    // friendly errors rather than crashing during host start in Azure.
    Console.WriteLine($"[ERROR] Unexpected error while preparing database: {ex.Message}");
}

if (!useOpenAI && !useClaude)
{
    Console.WriteLine("[WARN] WARNING: AI provider not configured!");
    Console.WriteLine("[WARN] To enable AI features:");
    Console.WriteLine("[WARN] - For Claude: set ANTHROPIC_API_KEY");
    Console.WriteLine("[WARN] - For OpenAI: set OPENAI_API_KEY");
    Console.WriteLine();
}

app.Run();