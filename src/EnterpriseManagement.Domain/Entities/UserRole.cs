namespace EnterpriseManagement.Domain.Entities;

/// <summary>
/// Join entity between <see cref="User"/> and <see cref="Role"/>.
/// </summary>
/// <remarks>
/// Modelled explicitly rather than as an EF Core skip navigation because it
/// carries payload (<see cref="AssignedAt"/>) and because "when did this person
/// become an admin?" is an audit question worth answering. It uses a composite
/// primary key (UserId, RoleId), which also enforces "a role only once per user"
/// without a separate unique index.
/// </remarks>
public class UserRole
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public DateTime AssignedAt { get; set; }
}
