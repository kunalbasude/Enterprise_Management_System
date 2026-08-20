namespace EnterpriseManagement.Application.Features.Departments.Dtos;

public class DepartmentDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Number of employees in this department.
    /// </summary>
    /// <remarks>
    /// Computed in the projection as a correlated COUNT, so the list endpoint
    /// stays a single query. Loading each department's Employees collection to
    /// count it in memory would be a textbook N+1: one query for the page plus
    /// one per row.
    /// </remarks>
    public int EmployeeCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
