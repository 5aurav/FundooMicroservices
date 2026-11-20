using FluentValidation;
using FluentValidation.Results;
using Fundoo.Common.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Fundoo.Common.Validation;

/// <summary>
/// Runs any registered <see cref="IValidator{T}"/> against bound action arguments
/// before the action executes, returning a <see cref="ValidationProblemDetails"/>
/// 400 response on failure. This replaces per-controller manual checks and
/// data annotations with a single, consistent validation path.
/// </summary>
public sealed class FluentValidationActionFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    public FluentValidationActionFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        foreach (var (key, value) in context.ActionArguments)
        {
            if (value is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(value.GetType());
            if (_serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = (IValidationContext)Activator.CreateInstance(
                typeof(ValidationContext<>).MakeGenericType(value.GetType()),
                value)!;

            var result = await validator.ValidateAsync(
                validationContext,
                context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                context.Result = CreateValidationProblem(context, result);
                return;
            }
        }

        await next();
    }

    private static IActionResult CreateValidationProblem(
        ActionExecutingContext context,
        ValidationResult result)
    {
        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest,
            Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
            Instance = context.HttpContext.Request.Path
        };

        var correlationId = CorrelationIdMiddleware.GetCorrelationId(context.HttpContext);
        if (correlationId is not null)
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        return new BadRequestObjectResult(problem);
    }
}
