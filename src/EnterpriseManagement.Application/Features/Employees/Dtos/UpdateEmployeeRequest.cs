namespace EnterpriseManagement.Application.Features.Employees.Dtos;

/// <summary>
/// Updates an employee.
/// </summary>
/// <remarks>
/// <see cref="CreateEmployeeRequest.EmployeeCode"/> is absent by design: the
/// business key must not change, because reports, exports and printed records
/// elsewhere already reference it.
/// </remarks>
public class UpdateEmployeeRequest
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string JobTitle { get; set; } = string.Empty;

    public DateOnly HireDate { get; set; }

    public int DepartmentId { get; set; }

    public bool IsActive { get; set; }

    public int? UserId { get; set; }
}
