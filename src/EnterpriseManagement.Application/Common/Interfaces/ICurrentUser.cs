namespace EnterpriseManagement.Application.Common.Interfaces;

/// <summary>
/// The caller behind the current request, read from the validated JWT.
/// </summary>
/// <remarks>
/// Application needs to know who is acting — for authorisation checks and audit
/// rows — without referencing <c>HttpContext</c>, which would drag ASP.NET Core
/// into the business layer and make every service require a web host to test.
/// The Api layer implements this over <c>IHttpContextAccessor</c>.
/// </remarks>
public interface ICurrentUser
{
    /// <summary>User id from the "sub" claim, or null when unauthenticated.</summary>
    int? UserId { get; }

    string? Email { get; }

    /// <summary>
    /// Linked employee id, when the account has one. This is what
    /// "is this my project?" checks compare against.
    /// </summary>
    int? EmployeeId { get; }

    IReadOnlyList<string> Roles { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);
}
