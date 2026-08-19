using EnterpriseManagement.Domain.Common;

namespace EnterpriseManagement.Domain.Entities;

/// <summary>
/// Membership of an employee on a project.
/// </summary>
/// <remarks>
/// A first-class entity rather than an EF Core skip navigation, because the
/// relationship carries data of its own: what the person does on the project and
/// when they joined or left. Any join table with payload must be modelled
/// explicitly — the moment you need a column on the relationship, the implicit
/// many-to-many stops being an option.
/// </remarks>
public class ProjectEmployee : BaseEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>
    /// What this person does on the project, e.g. "Backend Developer". Free text
    /// and distinct from <see cref="Role"/>, which is a security concept — a job
    /// title on a project grants no permissions.
    /// </summary>
    public string RoleOnProject { get; set; } = string.Empty;

    public DateTime AssignedAt { get; set; }

    /// <summary>
    /// Set instead of deleting the row when someone leaves the project, so the
    /// historical record of who worked on what survives.
    /// </summary>
    public DateTime? UnassignedAt { get; set; }

    /// <summary>True while the member is on the project.</summary>
    public bool IsCurrent => UnassignedAt is null;
}
