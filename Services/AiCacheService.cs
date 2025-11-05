using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using project.Data;
using project.Models;

namespace project.Services
{
    public interface IAiCacheService
    {
        Task<string?> GetCachedResponseAsync(string prompt, string? modelName = null);
        Task CacheResponseAsync(string prompt, string response, string? modelName = null, int tokenCount = 0, TimeSpan? expiresIn = null);
        Task<int> CleanupExpiredCacheAsync();
    }

    public class AiCacheService : IAiCacheService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AiCacheService> _logger;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public AiCacheService(IServiceScopeFactory scopeFactory, ILogger<AiCacheService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<string?> GetCachedResponseAsync(string prompt, string? modelName = null)
        {
            var hash = ComputeHash(prompt);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var cached = await context.AiResponseCaches
                .Where(c => c.PromptHash == hash)
                .Where(c => c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (cached != null)
            {
                _logger.LogInformation("Cache HIT for prompt hash: {Hash}", hash);

                // Zwiększ licznik trafień
                cached.HitCount++;
                await context.SaveChangesAsync();

                return cached.Response;
            }

            _logger.LogInformation("Cache MISS for prompt hash: {Hash}", hash);
            return null;
        }

        public async Task CacheResponseAsync(string prompt, string response, string? modelName = null, int tokenCount = 0, TimeSpan? expiresIn = null)
        {
            var hash = ComputeHash(prompt);

            // Use semaphore to prevent concurrent database access issues
            await _semaphore.WaitAsync();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Sprawdź, czy już istnieje
                var existing = await context.AiResponseCaches
                    .FirstOrDefaultAsync(c => c.PromptHash == hash);

                if (existing != null)
                {
                    _logger.LogInformation("Updating existing cache entry for hash: {Hash}", hash);
                    existing.Response = response;
                    existing.CreatedAt = DateTime.UtcNow;
                    existing.ExpiresAt = expiresIn.HasValue ? DateTime.UtcNow.Add(expiresIn.Value) : null;
                    existing.TokenCount = tokenCount;
                    existing.ModelName = modelName;
                }
                else
                {
                    _logger.LogInformation("Creating new cache entry for hash: {Hash}", hash);
                    var cacheEntry = new AiResponseCache
                    {
                        PromptHash = hash,
                        Response = response,
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = expiresIn.HasValue ? DateTime.UtcNow.Add(expiresIn.Value) : null,
                        ModelName = modelName,
                        TokenCount = tokenCount,
                        HitCount = 0
                    };

                    context.AiResponseCaches.Add(cacheEntry);
                }

                await context.SaveChangesAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<int> CleanupExpiredCacheAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var expired = await context.AiResponseCaches
                .Where(c => c.ExpiresAt != null && c.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            if (expired.Any())
            {
                _logger.LogInformation("Cleaning up {Count} expired cache entries", expired.Count);
                context.AiResponseCaches.RemoveRange(expired);
                await context.SaveChangesAsync();
            }

            return expired.Count;
        }

        private string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
