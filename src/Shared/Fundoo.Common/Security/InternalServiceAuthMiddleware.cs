using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Fundoo.Common.Security;

public class InternalServiceAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string? _expectedSecret;
    private readonly string _headerName;
    private readonly ILogger<InternalServiceAuthMiddleware> _logger;

    public InternalServiceAuthMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<InternalServiceAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _expectedSecret = configuration["InternalSecurity:SharedSecret"];
        _headerName = configuration["InternalSecurity:SharedSecretHeader"] ?? "X-Internal-Service-Key";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var requiresInternal = endpoint?.Metadata.GetMetadata<RequireInternalServiceAttribute>() != null;

        // If configured and required, check the internal service key
        if (requiresInternal && !string.IsNullOrEmpty(_expectedSecret))
        {
            if (!context.Request.Headers.TryGetValue(_headerName, out var receivedSecret) ||
                !string.Equals(receivedSecret, _expectedSecret, StringComparison.Ordinal))
            {
                _logger.LogWarning("Unauthorized direct microservice invocation blocked on {Path}", context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";

                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    type = "https://fundoo.com/errors/forbidden",
                    title = "Forbidden",
                    status = 403,
                    detail = "Direct microservice invocation blocked. Requests must originate through the API Gateway with valid internal credentials."
                }));
                return;
            }
        }

        await _next(context);
    }
}
