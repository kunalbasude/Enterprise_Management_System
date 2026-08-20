namespace EnterpriseManagement.Application.Features.Projects.Dtos;

/// <summary>One employee's membership of a project, including past membership.</summary>
public class ProjectMemberDto
{
    public int AssignmentId { get; set; }

    public int EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string JobTitle { get; set; } = string.Empty;

    /// <summary>What this person does on the project. Not a security role.</summary>
    public string RoleOnProject { get; set; } = string.Empty;

    public DateTime AssignedAt { get; set; }

    /// <summary>Set when the member left. Null while they are current.</summary>
    public DateTime? UnassignedAt { get; set; }

    public bool IsCurrent => UnassignedAt is null;
}
