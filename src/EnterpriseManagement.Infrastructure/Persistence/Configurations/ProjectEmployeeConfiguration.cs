using EnterpriseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseManagement.Infrastructure.Persistence.Configurations;

public class ProjectEmployeeConfiguration : IEntityTypeConfiguration<ProjectEmployee>
{
    public void Configure(EntityTypeBuilder<ProjectEmployee> builder)
    {
        builder.ToTable("project_employees");

        // A surrogate key rather than a composite (ProjectId, EmployeeId) key,
        // because history matters: someone can leave a project and rejoin later,
        // which would violate a composite primary key. The uniqueness rule we
        // actually want is narrower and is expressed by the filtered index below.
        builder.HasKey(pe => pe.Id);

        builder.HasOne(pe => pe.Project)
            .WithMany(p => p.TeamMembers)
            .HasForeignKey(pe => pe.ProjectId)
            // Cascade: membership rows have no meaning without the project.
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pe => pe.Employee)
            .WithMany(e => e.ProjectAssignments)
            .HasForeignKey(pe => pe.EmployeeId)
            // Restrict: deleting an employee with project history must fail.
            // Deactivate them instead; the record of who did what is the point.
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(pe => pe.RoleOnProject).IsRequired().HasMaxLength(100);
        builder.Property(pe => pe.AssignedAt).IsRequired();

        builder.Ignore(pe => pe.IsCurrent);

        // INDEX: unique on (ProjectId, EmployeeId) filtered to current members.
        // Enforces "an employee is on a project at most once at a time" while
        // still permitting historical rows where UnassignedAt is set. A plain
        // unique index would forbid rejoining after leaving, which is a real
        // scenario. The leading ProjectId also serves "who is on this project?".
        builder.HasIndex(pe => new { pe.ProjectId, pe.EmployeeId })
            .IsUnique()
            .HasFilter("unassigned_at IS NULL")
            .HasDatabaseName("ix_project_employees_project_id_employee_id_current");

        // INDEX: EmployeeId.
        // Answers the reverse question — "which projects am I on?" — which the
        // composite above cannot, since it leads with ProjectId.
        builder.HasIndex(pe => pe.EmployeeId)
            .HasDatabaseName("ix_project_employees_employee_id");
    }
}
