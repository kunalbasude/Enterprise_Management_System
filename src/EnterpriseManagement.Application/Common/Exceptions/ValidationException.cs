namespace EnterpriseManagement.Application.Common.Exceptions;

/// <summary>
/// Thrown when incoming request data fails validation. Maps to HTTP 400 with a
/// per-field error dictionary.
/// </summary>
/// <remarks>
/// Lives in Application rather than Domain because validating a request shape is
/// an application concern: the domain has no notion of an incoming DTO. Contrast
/// with <c>BusinessRuleViolationException</c>, which is about domain state and
/// returns 422.
/// </remarks>
public class ValidationException : Exception
{
    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IDictionary<string, string[]> Errors { get; }
}
