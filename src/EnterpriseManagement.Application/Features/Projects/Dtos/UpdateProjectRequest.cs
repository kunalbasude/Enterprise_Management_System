using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Application.Features.Projects.Dtos;

/// <summary>
/// Updates a project. The code is immutable, like the employee code.
/// </summary>
/// <remarks>
/// <see cref="ManagerEmployeeId"/> is included but only ADMIN may change it: a
/// manager reassigning their own project to someone else, or to themselves,
/// would be an authorisation bypass. That rule is enforced in the service.
/// </remarks>
public class UpdateProjectRequest
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ProjectStatus Status { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int ManagerEmployeeId { get; set; }
}
