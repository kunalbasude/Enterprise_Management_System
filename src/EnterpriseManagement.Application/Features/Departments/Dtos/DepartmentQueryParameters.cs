using EnterpriseManagement.Application.Common.Models;

namespace EnterpriseManagement.Application.Features.Departments.Dtos;

/// <summary>
/// Query string for the department list. Inherits paging, search and sorting.
/// </summary>
public class DepartmentQueryParameters : QueryParameters
{
    /// <summary>
    /// Fields that may be sorted on. Any other value falls back to the default.
    /// </summary>
    /// <remarks>
    /// Exposed as a constant so the API documentation and the validator both
    /// describe the same set, rather than drifting apart.
    /// </remarks>
    public static readonly IReadOnlyList<string> SortableFields =
        ["name", "createdAt", "employeeCount"];
}
