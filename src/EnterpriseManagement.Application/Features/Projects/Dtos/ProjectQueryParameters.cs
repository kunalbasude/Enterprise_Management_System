using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Application.Features.Projects.Dtos;

public class ProjectQueryParameters : QueryParameters
{
    /// <summary>Filter by lifecycle state. Served by ix_projects_status.</summary>
    public ProjectStatus? Status { get; set; }

    /// <summary>Filter to one manager. Served by ix_projects_manager_employee_id.</summary>
    public int? ManagerEmployeeId { get; set; }

    public static readonly IReadOnlyList<string> SortableFields =
        ["name", "code", "status", "startDate", "endDate", "createdAt"];
}
