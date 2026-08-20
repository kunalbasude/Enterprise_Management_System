namespace EnterpriseManagement.Application.Features.Employees.Dtos;

public class CreateEmployeeRequest
{
    /// <summary>
    /// Business key, e.g. "EMP-0042".
    /// </summary>
    /// <remarks>
    /// Supplied by the caller rather than generated. Auto-generation reads the
    /// current maximum and increments, which races under concurrency and
    /// produces duplicates unless it goes through a database sequence.
    /// Requiring it keeps the endpoint predictable and idempotent to reason
    /// about; a real HR system would use a Postgres sequence.
    /// </remarks>
    public string EmployeeCode { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string JobTitle { get; set; } = string.Empty;

    public DateOnly HireDate { get; set; }

    public int DepartmentId { get; set; }

    /// <summary>Optional login to link. Each account may back at most one employee.</summary>
    public int? UserId { get; set; }
}
