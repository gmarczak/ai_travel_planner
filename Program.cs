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
using project.Services.AI;
using Polly;
using Polly.CircuitBreaker;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

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
    // PRODUCTION: Use Azure App Service Configuration (Environment Variables)
    Console.WriteLine("[INFO] Production environment: Using Azure App Service Configuration");

    // Optional: Try Azure Key Vault if configured
    var keyVaultName = builder.Configuration["KeyVault:Name"];
    if (!string.IsNullOrEmpty(keyVaultName) &&
        keyVaultName != "your-keyvault-name-here" &&
        !keyVaultName.StartsWith("your-keyvault-name"))
    {
        try
        {
            var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
            builder.Configuration.AddAzureKeyVault(
                keyVaultUri,
                new DefaultAzureCredential()
            );
            Console.WriteLine($"[INFO] Loaded secrets from Azure Key Vault: {keyVaultName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Failed to connect to Key Vault: {ex.Message}");
            Console.WriteLine("[INFO] Continuing with App Service Configuration...");
        }
    }
    else
    {
        Console.WriteLine("[INFO] Key Vault not configured - using App Service Configuration only");
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
                 ?? builder.Configuration["GoogleMaps_ApiKey"]
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
            sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
            sql.CommandTimeout(120);
        }));
    // Factory (uses same options internally; no custom lambda to avoid scoped->singleton mismatch)
    // Removed AddDbContextFactory (not required; causing lifetime validation issue)
    Console.WriteLine("[INFO] Using SQL Server (Azure SQL Database)");
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=travelplanner.db";
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
    // Removed AddDbContextFactory (not required; causing lifetime validation issue)
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

// In Development relax password policy to allow simple seed password
if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<IdentityOptions>(o =>
    {
        o.Password.RequireDigit = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireLowercase = false;
        o.Password.RequiredLength = 6;
    });
}

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

// ADD RESPONSE COMPRESSION
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

// BACKGROUND PLAN GENERATION QUEUE + WORKER
builder.Services.AddSingleton<IPlanJobQueue, PlanGenerationQueue>();
builder.Services.AddHostedService<PlanGenerationWorker>();
builder.Services.AddHostedService<CacheCleanupWorker>();

// GLOBAL API RATE LIMITER (for cost control)
builder.Services.AddSingleton<ApiRateLimiter>();

// PROVIDER SELECTION (OPENAI OR CLAUDE)
var provider = builder.Configuration["AI:Provider"] ?? "OpenAI";
var openAiApiKey = builder.Configuration["OPENAI_API_KEY"] ?? builder.Configuration["OpenAI:ApiKey"];
var claudeApiKey = builder.Configuration["ANTHROPIC_API_KEY"] ?? builder.Configuration["Anthropic:ApiKey"] ?? builder.Configuration["CLAUDE_API_KEY"];
var useOpenAI = provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(openAiApiKey);
var useClaude = provider.Equals("Claude", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(claudeApiKey);

// SIGNALR
builder.Services.AddSignalR();

// RAZOR PAGES
builder.Services.AddRazorPages()
    .AddViewLocalization();

// Enable localization services and view localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new System.Globalization.CultureInfo("en-US"), new System.Globalization.CultureInfo("pl-PL") };
    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // Only use cookie provider - SetLanguage reads query string directly from Request.Query
    // so we don't need QueryStringRequestCultureProvider which would consume it
    options.RequestCultureProviders = new Microsoft.AspNetCore.Localization.IRequestCultureProvider[] {
        new CookieRequestCultureProvider()
    };
});

// ERROR MONITORING
builder.Services.AddScoped<IErrorMonitoringService, ErrorMonitoringService>();

// RESILIENCE & RETRY POLICIES
builder.Services.AddSingleton<ResiliencePolicyService>();

// CACHE HEADERS SERVICE
builder.Services.AddCacheHeaderService();

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

// Image Caption Service (AI-generated descriptions for activity images)
builder.Services.AddScoped<IImageCaptionService, ImageCaptionService>();

// Google Directions Service (road-based routes for map display)
builder.Services.AddHttpClient<IDirectionsService, GoogleDirectionsService>();

// Flight Service (search and display flight options)
builder.Services.AddHttpClient<IFlightService, FlightService>();

// PROMPT TEMPLATE SERVICE (external prompt files for easy modification)
builder.Services.AddScoped<PromptTemplateService>();

// AI ASSISTANT SERVICES (chat-based plan editing)
builder.Services.AddScoped<GptMiniAssistantService>();
builder.Services.AddScoped<Gpt41MiniAssistantService>();
builder.Services.AddScoped<IAiAssistantService, FallbackAssistantService>();
builder.Services.AddSingleton<AssistantTelemetryService>();
builder.Services.AddSingleton<AssistantRateLimiter>();
builder.Services.AddScoped<PlanDeltaApplier>();

// REGISTER AI SERVICES (with fallback support)
var enableFallback = builder.Configuration.GetValue<bool>("AI:EnableFallback", true);

// Register individual AI providers as IAiService
builder.Services.AddScoped<OpenAITravelService>();
builder.Services.AddScoped<IAiService>(sp => sp.GetRequiredService<OpenAITravelService>());

// Register AI Assistant (chat edit) services (stubs with fallback dispatcher)
builder.Services.AddScoped<GptMiniAssistantService>();
builder.Services.AddScoped<Gpt41MiniAssistantService>();
builder.Services.AddScoped<IAiAssistantService, FallbackAssistantService>();
builder.Services.AddScoped<PlanDeltaApplier>();
builder.Services.AddSingleton<AssistantTelemetryService>();
builder.Services.AddSingleton<AssistantRateLimiter>();

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
        builder.Services.AddScoped<ITravelService, OpenAITravelService>();
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
    builder.Services.AddScoped<ITravelService, OpenAITravelService>();
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

// Apply request localization early in the pipeline
app.UseRequestLocalization(app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value);

// Ensure SQLite schema is created in Development without applying SQL Server migrations
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<project.Data.ApplicationDbContext>();
        if (db.Database.EnsureCreated())
        {
            Console.WriteLine("[DEV] SQLite database schema created (EnsureCreated). Migrations are bypassed.");
        }
        else
        {
            Console.WriteLine("[DEV] SQLite database schema already exists.");
            // Ensure new tables added after initial creation (EnsureCreated does not add them later)
            try
            {
                var routeTableExists = db.Database.ExecuteSqlRaw(
                    "SELECT 1 FROM sqlite_master WHERE type='table' AND name='RoutePolylines';") == 1; // returns number of rows affected (always 0)
            }
            catch
            {
                // If querying sqlite_master fails or table missing, attempt to create it
                Console.WriteLine("[DEV] Ensuring 'RoutePolylines' table exists...");
                try
                {
                    db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS RoutePolylines (
                        Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        RouteKey TEXT NOT NULL,
                        EncodedPolyline TEXT NOT NULL,
                        CachedAt TEXT NOT NULL,
                        UsageCount INTEGER NOT NULL,
                        CONSTRAINT IX_RoutePolylines_RouteKey UNIQUE (RouteKey)
                    );");
                    db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_RoutePolylines_CachedAt ON RoutePolylines (CachedAt);");
                    Console.WriteLine("[DEV] 'RoutePolylines' table ensured.");
                }
                catch (Exception tableEx)
                {
                    Console.WriteLine($"[DEV][WARN] Failed to create RoutePolylines table manually: {tableEx.Message}");
                }
            }
        }

        // Seed admin account (email/username: admin@local, password: adminadmin)
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<project.Models.ApplicationUser>>();
        var existingByEmail = userManager.FindByEmailAsync("admin@local").GetAwaiter().GetResult();
        var existingByName = userManager.FindByNameAsync("admin").GetAwaiter().GetResult();

        if (existingByEmail == null && existingByName == null)
        {
            var admin = new project.Models.ApplicationUser
            {
                UserName = "admin@local",
                Email = "admin@local",
                EmailConfirmed = true,
                IsAdmin = true,
                FullName = "Administrator",
                CreatedAt = DateTime.UtcNow
            };
            var createRes = userManager.CreateAsync(admin, "adminadmin").GetAwaiter().GetResult();
            if (createRes.Succeeded)
            {
                Console.WriteLine("[DEV] Seeded admin user 'admin@local' with default password 'adminadmin'.");
            }
            else
            {
                Console.WriteLine("[DEV][WARN] Failed to create admin user: " + string.Join("; ", createRes.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            // Normalize: if 'admin' exists with username 'admin', rename to 'admin@local'
            var userToNormalize = existingByEmail ?? existingByName;
            if (userToNormalize != null)
            {
                if (string.IsNullOrWhiteSpace(userToNormalize.Email))
                {
                    userToNormalize.Email = "admin@local";
                    userToNormalize.EmailConfirmed = true;
                }
                if (!string.Equals(userToNormalize.UserName, "admin@local", StringComparison.OrdinalIgnoreCase))
                {
                    var setName = userManager.SetUserNameAsync(userToNormalize, "admin@local").GetAwaiter().GetResult();
                    if (!setName.Succeeded)
                    {
                        Console.WriteLine("[DEV][WARN] Failed to update admin username: " + string.Join("; ", setName.Errors.Select(e => e.Description)));
                    }
                }
                if (!userToNormalize.IsAdmin)
                {
                    userToNormalize.IsAdmin = true;
                }
                var updateRes = userManager.UpdateAsync(userToNormalize).GetAwaiter().GetResult();
                if (updateRes.Succeeded)
                {
                    Console.WriteLine("[DEV] Admin account normalized as 'admin@local' and ensured IsAdmin=true.");
                }
                else
                {
                    Console.WriteLine("[DEV][WARN] Failed to update admin user: " + string.Join("; ", updateRes.Errors.Select(e => e.Description)));
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DEV][ERROR] Failed to EnsureCreated: {ex.Message}");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// ERROR HANDLING MIDDLEWARE
app.UseErrorHandling();

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseCacheHeaders();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache static files for 30 days
        const int durationInSeconds = 60 * 60 * 24 * 30;
        ctx.Context.Response.Headers.Append("Cache-Control", $"public,max-age={durationInSeconds}");
    }
});
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// MAP SIGNALR HUBS
app.MapHub<project.Hubs.PlanGenerationHub>("/hubs/planGeneration");
app.MapHub<project.Hubs.AssistantChatHub>("/hubs/assistantChat");

app.MapRazorPages();

// Small endpoint to expose client-side config such as Google Maps API key
// This returns only non-sensitive configuration values intended for client use.
app.MapGet("/__config/maps", (IConfiguration config) =>
{
    var key = config["GoogleMaps:ApiKey"]
           ?? config["GoogleMaps_ApiKey"]
           ?? config["GOOGLE_MAPS_API_KEY"]
           ?? string.Empty;
    Console.WriteLine($"[DEBUG] Maps config endpoint called - Key loaded: {(string.IsNullOrEmpty(key) ? "EMPTY" : "***" + key.Substring(Math.Max(0, key.Length - 4)))}");
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
                // Development: ensure RoutePolylines table exists (added after initial schema)
                try
                {
                    db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS RoutePolylines (
                        Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        RouteKey TEXT NOT NULL,
                        EncodedPolyline TEXT NOT NULL,
                        CachedAt TEXT NOT NULL,
                        UsageCount INTEGER NOT NULL,
                        CONSTRAINT IX_RoutePolylines_RouteKey UNIQUE (RouteKey)
                    );");
                    db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS IX_RoutePolylines_CachedAt ON RoutePolylines (CachedAt);");
                    Console.WriteLine("[DATABASE] RoutePolylines table ensured.");
                }
                catch (Exception rpEx)
                {
                    Console.WriteLine($"[DEV][WARN] Failed ensuring RoutePolylines table: {rpEx.Message}");
                }

                // Ensure TransportMode column exists in TravelPlans (added after initial schema)
                try
                {
                    db.Database.ExecuteSqlRaw("SELECT TransportMode FROM TravelPlans LIMIT 1;");
                    Console.WriteLine("[DATABASE] TransportMode column exists.");
                }
                catch
                {
                    db.Database.ExecuteSqlRaw("ALTER TABLE TravelPlans ADD COLUMN TransportMode TEXT NULL;");
                    Console.WriteLine("[DATABASE] Added TransportMode column to TravelPlans.");
                }

                // Ensure DepartureLocation column exists in TravelPlans
                try
                {
                    db.Database.ExecuteSqlRaw("SELECT DepartureLocation FROM TravelPlans LIMIT 1;");
                    Console.WriteLine("[DATABASE] DepartureLocation column exists.");
                }
                catch
                {
                    db.Database.ExecuteSqlRaw("ALTER TABLE TravelPlans ADD COLUMN DepartureLocation TEXT NULL;");
                    Console.WriteLine("[DATABASE] Added DepartureLocation column to TravelPlans.");
                }
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

                // Seed/ensure admin account in Production using secure secrets
                try
                {
                    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                    var adminEmail = config["Admin:Email"] ?? config["ADMIN_EMAIL"]; // Prefer hierarchical config
                    var adminPassword = config["Admin:Password"] ?? config["ADMIN_PASSWORD"]; // Set via Key Vault or env

                    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
                    {
                        var existingByEmail = await userManager.FindByEmailAsync(adminEmail);
                        var existingByName = await userManager.FindByNameAsync(adminEmail);
                        var user = existingByEmail ?? existingByName;

                        if (user == null)
                        {
                            user = new ApplicationUser
                            {
                                UserName = adminEmail,
                                Email = adminEmail,
                                EmailConfirmed = true,
                                IsAdmin = true,
                                FullName = "Administrator"
                            };
                            var create = await userManager.CreateAsync(user, adminPassword);
                            if (create.Succeeded)
                            {
                                Console.WriteLine("[PROD] Admin user created from configuration (email). IsAdmin=true.");
                            }
                            else
                            {
                                Console.WriteLine("[PROD][WARN] Failed to create admin user: " + string.Join("; ", create.Errors.Select(e => e.Description)));
                            }
                        }
                        else
                        {
                            // Normalize username/email and ensure IsAdmin=true
                            if (!string.Equals(user.UserName, adminEmail, StringComparison.OrdinalIgnoreCase))
                            {
                                var setU = await userManager.SetUserNameAsync(user, adminEmail);
                                if (!setU.Succeeded)
                                {
                                    Console.WriteLine("[PROD][WARN] Failed to update admin username: " + string.Join("; ", setU.Errors.Select(e => e.Description)));
                                }
                            }
                            if (!string.Equals(user.Email, adminEmail, StringComparison.OrdinalIgnoreCase))
                            {
                                user.Email = adminEmail;
                                user.EmailConfirmed = true;
                            }
                            if (!user.IsAdmin)
                            {
                                user.IsAdmin = true;
                            }
                            var upd = await userManager.UpdateAsync(user);
                            if (upd.Succeeded)
                            {
                                Console.WriteLine("[PROD] Admin user ensured and normalized. IsAdmin=true.");
                            }
                            else
                            {
                                Console.WriteLine("[PROD][WARN] Failed to update admin user: " + string.Join("; ", upd.Errors.Select(e => e.Description)));
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("[PROD][INFO] Admin credentials not configured (Admin:Email/Admin:Password). Skipping admin seed.");
                    }
                }
                catch (Exception seedEx)
                {
                    Console.WriteLine($"[PROD][WARN] Admin seeding skipped due to error: {seedEx.Message}");
                }
            }
        }
        catch (Exception migEx)
        {
            // Log migration errors but allow Azure to handle startup failures
            Console.WriteLine($"[ERROR] Migration/Ensure step failed: {migEx.Message}");
            if (app.Environment.IsDevelopment())
            {
                Console.WriteLine($"[DEV] Stack trace: {migEx.StackTrace}");
            }
        }
    }
}
catch (Exception ex)
{
    // Log to console - Azure App Service will capture these logs
    Console.WriteLine($"[ERROR] Unexpected error while preparing database: {ex.Message}");
    if (app.Environment.IsDevelopment())
    {
        Console.WriteLine($"[DEV] Stack trace: {ex.StackTrace}");
    }
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