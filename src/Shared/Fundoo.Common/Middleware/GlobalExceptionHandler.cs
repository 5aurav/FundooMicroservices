using System.Diagnostics;
using Fundoo.Common.Exceptions;
using Fundoo.Common.Logging;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Fundoo.Common.Middleware;

/// <summary>
/// Centralized exception boundary. Converts unhandled exceptions into
/// RFC 7807 <see cref="ProblemDetails"/> responses so every endpoint fails the
/// same way, while keeping internal exception messages out of 500 responses.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = MapStatusCode(exception);
        var correlationId = CorrelationIdMiddleware.GetCorrelationId(httpContext);

        _logger.LogError(
            exception,
            "Unhandled exception | Path:{Path} Status:{Status} CorrelationId:{CorrelationId}",
            httpContext.Request.Path,
            statusCode,
            correlationId ?? "-");

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(exception),
            Detail = exception is NotFoundException or ValidationException
                ? exception.Message
                : "An unexpected error occurred.",
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (correlationId is not null)
        {
            problemDetails.Extensions["correlationId"] = correlationId;
        }

        if (exception is ValidationException validationException &&
            validationException.Errors.Count > 0)
        {
            problemDetails.Extensions["errors"] = validationException.Errors;
        }

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            cancellationToken);

        return true;
    }

    private static int MapStatusCode(Exception exception) => exception switch
    {
        ValidationException => StatusCodes.Status400BadRequest,
        NotFoundException => StatusCodes.Status404NotFound,
        UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string GetTitle(Exception exception) => exception switch
    {
        ValidationException => "Validation failed",
        NotFoundException => "Resource not found",
        UnauthorizedAccessException => "Unauthorized",
        _ => "Internal server error"
    };
}
