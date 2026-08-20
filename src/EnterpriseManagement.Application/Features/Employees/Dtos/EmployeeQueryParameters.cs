using EnterpriseManagement.Application.Common.Models;

namespace EnterpriseManagement.Application.Features.Employees.Dtos;

public class EmployeeQueryParameters : QueryParameters
{
    /// <summary>Filter to one department. Served by ix_employees_department_id.</summary>
    public int? DepartmentId { get; set; }

    /// <summary>Filter by employment status. Null returns both.</summary>
    public bool? IsActive { get; set; }

    public static readonly IReadOnlyList<string> SortableFields =
        ["employeeCode", "firstName", "lastName", "email", "jobTitle", "hireDate", "createdAt"];
}
