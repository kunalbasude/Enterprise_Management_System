namespace EnterpriseManagement.Application.Features.Employees.Dtos;

/// <summary>An employee as it appears in list results.</summary>
/// <remarks>
/// Flattens the department name rather than nesting a DepartmentDto. The list
/// needs one string, and projecting it directly avoids serialising an object
/// graph the client would only read one field from.
/// </remarks>
public class EmployeeListDto
{
    public int Id { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>Composed in the projection so the database does the concatenation.</summary>
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string JobTitle { get; set; } = string.Empty;

    public DateOnly HireDate { get; set; }

    public bool IsActive { get; set; }

    public int DepartmentId { get; set; }

    public string DepartmentName { get; set; } = string.Empty;

    /// <summary>Linked login account, when one exists.</summary>
    public int? UserId { get; set; }

    /// <summary>Number of projects this employee is currently a member of.</summary>
    public int ActiveProjectCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
