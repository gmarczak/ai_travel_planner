namespace project.Services
{
    /// <summary>
    /// Cache duration constants
    /// </summary>
    public enum CacheDuration
    {
        VeryShort = 300,        // 5 minutes
        Short = 900,            // 15 minutes
        Medium = 3600,          // 1 hour
        Long = 86400,           // 1 day
        VeryLong = 604800       // 1 week
    }

    /// <summary>
    /// Service for managing HTTP caching headers and strategies
    /// </summary>
    public interface ICacheHeaderService
    {
        void SetCacheHeaders(HttpContext context, CacheDuration duration);
        void SetNoCacheHeaders(HttpContext context);
        void SetPublicCacheHeaders(HttpContext context, int maxAgeSeconds);
        void SetPrivateCacheHeaders(HttpContext context, int maxAgeSeconds);
    }

    public class CacheHeaderService : ICacheHeaderService
    {
        private readonly ILogger<CacheHeaderService> _logger;

        public CacheHeaderService(ILogger<CacheHeaderService> logger)
        {
            _logger = logger;
        }

        public void SetCacheHeaders(HttpContext context, CacheDuration duration)
        {
            SetPublicCacheHeaders(context, (int)duration);
        }

        public void SetNoCacheHeaders(HttpContext context)
        {
            var headers = context.Response.Headers;
            headers["Cache-Control"] = "no-store, no-cache, must-revalidate, proxy-revalidate";
            headers["Pragma"] = "no-cache";
            headers["Expires"] = "0";

            _logger.LogDebug("Set no-cache headers for response");
        }

        public void SetPublicCacheHeaders(HttpContext context, int maxAgeSeconds)
        {
            var headers = context.Response.Headers;
            headers["Cache-Control"] = $"public, max-age={maxAgeSeconds}";
            headers["ETag"] = GenerateETag();

            _logger.LogDebug($"Set public cache headers for {maxAgeSeconds}s");
        }

        public void SetPrivateCacheHeaders(HttpContext context, int maxAgeSeconds)
        {
            var headers = context.Response.Headers;
            headers["Cache-Control"] = $"private, max-age={maxAgeSeconds}";
            headers["ETag"] = GenerateETag();

            _logger.LogDebug($"Set private cache headers for {maxAgeSeconds}s");
        }

        private string GenerateETag()
        {
            return $"\"{Guid.NewGuid().ToString("N").Substring(0, 8)}\"";
        }
    }

    /// <summary>
    /// Middleware to add cache headers to responses
    /// </summary>
    public class CacheHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ICacheHeaderService _cacheHeaderService;
        private readonly ILogger<CacheHeadersMiddleware> _logger;

        public CacheHeadersMiddleware(
            RequestDelegate next,
            ICacheHeaderService cacheHeaderService,
            ILogger<CacheHeadersMiddleware> logger)
        {
            _next = next;
            _cacheHeaderService = cacheHeaderService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Add cache headers based on path
            if (IsStaticContent(context.Request.Path))
            {
                _cacheHeaderService.SetPublicCacheHeaders(context, (int)CacheDuration.VeryLong);
            }
            else if (IsApiEndpoint(context.Request.Path))
            {
                // API responses should not be cached by browsers/proxies by default
                _cacheHeaderService.SetNoCacheHeaders(context);
            }

            await _next(context);
        }

        private bool IsStaticContent(PathString path)
        {
            var pathStr = path.Value?.ToLower() ?? "";
            return pathStr.Contains("/css/") ||
                   pathStr.Contains("/js/") ||
                   pathStr.Contains("/images/") ||
                   pathStr.Contains("/lib/") ||
                   pathStr.EndsWith(".css") ||
                   pathStr.EndsWith(".js") ||
                   pathStr.EndsWith(".png") ||
                   pathStr.EndsWith(".jpg") ||
                   pathStr.EndsWith(".gif") ||
                   pathStr.EndsWith(".svg") ||
                   pathStr.EndsWith(".woff2");
        }

        private bool IsApiEndpoint(PathString path)
        {
            var pathStr = path.Value?.ToLower() ?? "";
            return pathStr.Contains("/api/") || pathStr.Contains("/hub/");
        }
    }

    /// <summary>
    /// Extension methods for cache header middleware
    /// </summary>
    public static class CacheHeaderExtensions
    {
        public static IServiceCollection AddCacheHeaderService(this IServiceCollection services)
        {
            services.AddSingleton<ICacheHeaderService, CacheHeaderService>();
            return services;
        }

        public static IApplicationBuilder UseCacheHeaders(this IApplicationBuilder app)
        {
            return app.UseMiddleware<CacheHeadersMiddleware>();
        }
    }
}
