using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Application.Features.Projects.Dtos;

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique business key, e.g. "PRJ-2026-01".</summary>
    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ProjectStatus Status { get; set; } = ProjectStatus.Planned;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    /// <summary>The employee accountable for the project. Drives manager authorisation.</summary>
    public int ManagerEmployeeId { get; set; }
}
