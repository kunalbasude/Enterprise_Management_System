using EnterpriseManagement.Domain.Common;

namespace EnterpriseManagement.Domain.Entities;

/// <summary>
/// An HR record: a person the organisation employs.
/// </summary>
/// <remarks>
/// Separate from <see cref="User"/>, which is the login. The link is optional in
/// both directions: an employee may have no account yet, and an account may
/// belong to nobody on payroll.
/// </remarks>
public class Employee : BaseEntity, IAuditableEntity
{
    /// <summary>
    /// Human-facing business key, e.g. "EMP-0042". Unique, and searchable —
    /// it is what staff actually quote to each other, unlike the surrogate id.
    /// </summary>
    public string EmployeeCode { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Work contact address. Distinct from <see cref="User.Email"/>, which is the
    /// login identifier: employees without an account still need an address, and
    /// this is the one exposed in employee search results.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string JobTitle { get; set; } = string.Empty;

    /// <summary>
    /// A calendar date with no meaningful time component, so <see cref="DateOnly"/>
    /// rather than <see cref="DateTime"/>. Npgsql maps it to <c>date</c>, which
    /// removes any chance of a timezone shifting someone's start date by a day.
    /// </summary>
    public DateOnly HireDate { get; set; }

    /// <summary>False once the person leaves. The row is retained so historical tasks stay attributable.</summary>
    public bool IsActive { get; set; } = true;

    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    /// <summary>Optional link to a login. Null for staff with no system access.</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Projects this employee is a member of.</summary>
    public ICollection<ProjectEmployee> ProjectAssignments { get; set; } = new List<ProjectEmployee>();

    /// <summary>Projects this employee manages.</summary>
    public ICollection<Project> ManagedProjects { get; set; } = new List<Project>();

    /// <summary>Tasks currently assigned to this employee.</summary>
    public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Convenience for display and sorting. Not persisted — computed on read.</summary>
    public string FullName => $"{FirstName} {LastName}";
}
