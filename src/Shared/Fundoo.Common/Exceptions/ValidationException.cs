namespace Fundoo.Common.Exceptions;

/// <summary>
/// Thrown by application/domain code when a business-rule validation fails.
/// Maps to HTTP 400. Input-shape validation is handled by FluentValidation;
/// this exception is for invariants that cannot be checked from request DTOs.
/// </summary>
public sealed class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]>(
            errors,
            StringComparer.OrdinalIgnoreCase);
    }
}
