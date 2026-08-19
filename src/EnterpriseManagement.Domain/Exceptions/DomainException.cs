namespace EnterpriseManagement.Domain.Exceptions;

/// <summary>
/// Base for exceptions that represent an expected, meaningful failure rather
/// than a bug.
/// </summary>
/// <remarks>
/// The distinction is what lets the exception middleware behave correctly: a
/// <see cref="DomainException"/> maps to a specific 4xx status and its message
/// is safe to return to the caller, whereas any other exception is an
/// unanticipated failure that must be logged in full and reported as a bare 500
/// with no detail.
/// </remarks>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}
