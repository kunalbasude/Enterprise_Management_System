using EnterpriseManagement.Domain.Entities;

namespace EnterpriseManagement.Application.Common.Interfaces;

/// <summary>Issues signed access tokens.</summary>
public interface IJwtTokenGenerator
{
    /// <param name="user">The authenticated user.</param>
    /// <param name="roles">Role names to embed as role claims.</param>
    /// <param name="employeeId">Linked employee id, when one exists. Used by resource authorisation.</param>
    /// <returns>The compact JWT and its absolute UTC expiry.</returns>
    (string Token, DateTime ExpiresAt) GenerateToken(User user, IEnumerable<string> roles, int? employeeId);
}
