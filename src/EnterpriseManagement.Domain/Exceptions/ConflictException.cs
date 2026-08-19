namespace EnterpriseManagement.Domain.Exceptions;

/// <summary>
/// Thrown when a request collides with existing state, such as reusing an email
/// or an employee code. Maps to HTTP 409.
/// </summary>
/// <remarks>
/// Distinct from a validation failure (400): the request is well-formed and the
/// values are individually legal — it is the current contents of the database
/// that make it impossible.
/// </remarks>
public class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }
}
