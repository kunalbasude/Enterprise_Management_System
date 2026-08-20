namespace EnterpriseManagement.Application.Features.Projects.Dtos;

public class AssignEmployeeRequest
{
    public int EmployeeId { get; set; }

    /// <summary>Job description on this project, e.g. "Backend Developer".</summary>
    public string RoleOnProject { get; set; } = string.Empty;
}
