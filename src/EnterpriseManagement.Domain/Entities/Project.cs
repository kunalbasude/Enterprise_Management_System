using EnterpriseManagement.Domain.Common;
using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Domain.Entities;

/// <summary>A unit of work with a manager, a team and a set of tasks.</summary>
public class Project : BaseEntity, IAuditableEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Short unique business key, e.g. "PRJ-2026-01".</summary>
    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ProjectStatus Status { get; set; } = ProjectStatus.Planned;

    public DateOnly StartDate { get; set; }

    /// <summary>Planned end date. Null while the project is open-ended.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// The employee accountable for this project. This is the field every MANAGER
    /// authorisation check resolves against: "may this user act on this project?"
    /// reduces to "is their employee id this project's manager id?".
    /// </summary>
    public int ManagerEmployeeId { get; set; }
    public Employee Manager { get; set; } = null!;

    /// <summary>Team membership, with assignment history.</summary>
    public ICollection<ProjectEmployee> TeamMembers { get; set; } = new List<ProjectEmployee>();

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
