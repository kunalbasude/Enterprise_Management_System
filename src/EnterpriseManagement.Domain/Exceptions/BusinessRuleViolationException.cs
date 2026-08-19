namespace EnterpriseManagement.Domain.Exceptions;

/// <summary>
/// Thrown when a request is syntactically valid but breaks a business rule, such
/// as moving a cancelled task back to in-progress. Maps to HTTP 422.
/// </summary>
/// <remarks>
/// 422 rather than 400 draws a line worth drawing: 400 means "I could not
/// understand or parse this", 422 means "I understood it perfectly and the
/// domain refuses". That distinction tells a client whether retrying with the
/// same payload could ever succeed.
/// </remarks>
public class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message) : base(message)
    {
    }
}
