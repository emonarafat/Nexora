namespace Nexora.SearchAPI.Security;

public class ApiKeyAuthenticationMiddleware(RequestDelegate next, IConfiguration config, ILogger<ApiKeyAuthenticationMiddleware> logger)
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private readonly HashSet<string> _validApiKeys = LoadValidApiKeys(config);

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for health check and metrics endpoints
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/metrics"))
        {
            await next(context);
            return;
        }

        // Skip authentication for Swagger in development
        IWebHostEnvironment? env = null;
        var serviceProvider = context.RequestServices;
        if (serviceProvider != null)
        {
            env = serviceProvider.GetService<IWebHostEnvironment>();
        }
        if (env?.IsDevelopment() == true &&
            (context.Request.Path.StartsWithSegments("/swagger") ||
             context.Request.Path.Value == "/"))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedApiKey) ||
            string.IsNullOrWhiteSpace(providedApiKey))
        {
            logger.LogWarning("API key missing from request. Path: {Path}, IP: {IP}",
                context.Request.Path,
                context.Connection.RemoteIpAddress);

            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "API key required",
                message = "Please provide a valid API key in the X-API-Key header"
            });
            return;
        }

        if (!_validApiKeys.Contains(providedApiKey.ToString()))
        {
            logger.LogWarning("Invalid API key provided. Path: {Path}, IP: {IP}, KeyPrefix: {KeyPrefix}",
                context.Request.Path,
                context.Connection.RemoteIpAddress,
                providedApiKey.ToString()[..Math.Min(8, providedApiKey.ToString().Length)]);

            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Invalid API key",
                message = "The provided API key is not valid"
            });
            return;
        }

        // Store API key identifier in HttpContext for rate limiting and logging
        context.Items["ApiKey"] = providedApiKey.ToString();

        logger.LogDebug("API key validated successfully for path {Path}", context.Request.Path);

        await next(context);
    }

    private static HashSet<string> LoadValidApiKeys(IConfiguration config)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        // Load from configuration section
        var apiKeysSection = config.GetSection("ApiKeys:ValidKeys").Get<string[]>();
        if (apiKeysSection != null)
        {
            foreach (var key in apiKeysSection)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    keys.Add(key);
                }
            }
        }

        // Also support comma-separated environment variable for easier Kubernetes secrets
        var envKeys = config["ApiKeys:ValidKeys:Csv"];
        if (!string.IsNullOrWhiteSpace(envKeys))
        {
            foreach (var key in envKeys.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                keys.Add(key.Trim());
            }
        }

        return keys;
    }
}
