namespace EnterpriseManagement.Domain.Exceptions;

/// <summary>
/// Thrown when the caller is authenticated but not permitted to perform this
/// action on this resource. Maps to HTTP 403.
/// </summary>
/// <remarks>
/// <para>
/// Distinct from <see cref="UnauthorizedException"/> (401), which means the
/// caller's identity is unknown or unproven. 403 means the identity is known
/// and the answer is still no — re-authenticating will not help, so a client
/// must not retry with fresh credentials.
/// </para>
/// <para>
/// Used for resource-based decisions that a declarative policy cannot express,
/// such as "this manager does not own this project". Policy failures on
/// [Authorize] attributes produce a 403 through the framework instead and never
/// reach this type.
/// </para>
/// </remarks>
public class ForbiddenException : DomainException
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
