using EnterpriseManagement.Domain.Common;

namespace EnterpriseManagement.Domain.Entities;

/// <summary>A business unit that employees belong to.</summary>
public class Department : BaseEntity, IAuditableEntity
{
    /// <summary>Unique department name, e.g. "Engineering".</summary>
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
