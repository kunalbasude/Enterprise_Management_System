using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Application.Features.Tasks.Dtos;

public class TaskQueryParameters : QueryParameters
{
    /// <summary>Filter by workflow state. Served by the composite task indexes.</summary>
    public TaskItemStatus? Status { get; set; }

    public TaskPriority? Priority { get; set; }

    /// <summary>Leading column of ix_tasks_project_id_status.</summary>
    public int? ProjectId { get; set; }

    /// <summary>Leading column of ix_tasks_assigned_employee_id_status.</summary>
    public int? AssignedEmployeeId { get; set; }

    /// <summary>Only tasks past their due date and not yet finished.</summary>
    public bool? IsOverdue { get; set; }

    public static readonly IReadOnlyList<string> SortableFields =
        ["title", "status", "priority", "dueDate", "createdAt"];
}
