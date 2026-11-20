using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using NLog;

namespace Fundoo.Common.Logging;

/// <summary>
/// Propagates a correlation identifier across the request lifecycle so a single
/// request can be traced through every service and log entry. Reads
/// <c>X-Correlation-Id</c> from the incoming request when present; otherwise
/// generates one. Echoes it on the response, exposes it via
/// <see cref="HttpContext.Items"/>, and pushes it into NLog's
/// <c>ScopeContext</c> so every log line renders <c>${scopeproperty:CorrelationId}</c>.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string CorrelationIdHeader = "X-Correlation-Id";

    private const string CorrelationIdItemKey = "__fundoo_correlation_id";
    private const string CorrelationIdScopeKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers
            .TryGetValue(CorrelationIdHeader, out var incoming) &&
            !string.IsNullOrWhiteSpace(incoming)
                ? incoming.ToString()
                : Guid.NewGuid().ToString("N");

        context.Items[CorrelationIdItemKey] = correlationId;

        if (!context.Response.HasStarted)
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
        }

        Activity.Current?.SetTag(CorrelationIdScopeKey, correlationId);

        using (ScopeContext.PushProperty(CorrelationIdScopeKey, correlationId))
        {
            await _next(context);
        }
    }

    /// <summary>Returns the correlation id for the current request, if set.</summary>
    public static string? GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(CorrelationIdItemKey, out var value) &&
        value is string correlationId
            ? correlationId
            : null;
}
