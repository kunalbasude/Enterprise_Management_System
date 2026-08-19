using EnterpriseManagement.Domain.Common;

namespace EnterpriseManagement.Domain.Entities;

/// <summary>
/// An authentication identity: something that can log in.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="Employee"/>. A user may exist without an
/// employee record (a system or contractor admin account), and an employee record
/// must survive its user being deactivated, because tasks and audit rows still
/// reference it.
/// </remarks>
public class User : BaseEntity, IAuditableEntity
{
    /// <summary>Login identifier. Unique, stored lower-cased for case-insensitive login.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// BCrypt hash including its per-user salt and work factor. The plaintext
    /// password is never stored, logged, or returned by any endpoint.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Display name, kept on the user so accounts without an employee record still have one.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Soft disable. Preferred over deleting: a deleted user would orphan audit
    /// rows that must remain attributable.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Roles held by this user. A user may hold more than one.</summary>
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    /// <summary>The HR record for this login, when one exists.</summary>
    public Employee? Employee { get; set; }
}
