using EnterpriseManagement.Domain.Common;

namespace EnterpriseManagement.Domain.Entities;

/// <summary>
/// A named authorisation role. Seeded with ADMIN, MANAGER and EMPLOYEE.
/// </summary>
/// <remarks>
/// A table rather than an enum column because a user can legitimately hold more
/// than one role, which a single column cannot express. Role names become JWT
/// role claims verbatim, so <see cref="Name"/> is treated as a stable contract
/// and stored upper-case.
/// </remarks>
public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
