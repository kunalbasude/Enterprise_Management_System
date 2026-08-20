using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Application.Features.Projects.Dtos;

public class ProjectListDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ProjectStatus Status { get; set; }

    /// <summary>
    /// The enum name, so clients are not coupled to the numeric storage value.
    /// </summary>
    /// <remarks>
    /// The column is an int for index size and comparison cost, but "Active" is
    /// what a client should branch on. Sending only the number would force every
    /// consumer to hardcode a mapping that silently breaks if the enum changes.
    /// </remarks>
    public string StatusName => Status.ToString();

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int ManagerEmployeeId { get; set; }

    public string ManagerName { get; set; } = string.Empty;

    /// <summary>Members with no UnassignedAt, i.e. currently on the project.</summary>
    public int TeamSize { get; set; }

    public int TaskCount { get; set; }

    public int CompletedTaskCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
