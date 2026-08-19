namespace EnterpriseManagement.Domain.Exceptions;

/// <summary>
/// Thrown when credentials are missing or invalid. Maps to HTTP 401.
/// </summary>
/// <remarks>
/// <para>
/// 401 means "I do not know who you are"; 403 means "I know exactly who you are
/// and you may not do this". Returning 403 for a bad password would tell an
/// attacker the account exists.
/// </para>
/// <para>
/// The message is deliberately identical for a missing account, a wrong password
/// and a disabled account. Distinguishing them turns login into an account
/// enumeration oracle.
/// </para>
/// </remarks>
public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}
