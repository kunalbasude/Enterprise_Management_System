using EnterpriseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseManagement.Infrastructure.Persistence.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(4000);

        builder.Property(t => t.Status).IsRequired().HasConversion<int>();
        builder.Property(t => t.Priority).IsRequired().HasConversion<int>();

        // precision 6, scale 2 -> up to 9999.99 hours. Explicit because the
        // PostgreSQL default for decimal is unbounded numeric, which is slower
        // and permits nonsense values like 40.000000001 hours.
        builder.Property(t => t.EstimatedHours).HasPrecision(6, 2);

        builder.HasOne(t => t.Project)
            .WithMany(p => p.Tasks)
            .HasForeignKey(t => t.ProjectId)
            // Cascade: a task cannot outlive its project.
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.AssignedEmployee)
            .WithMany(e => e.AssignedTasks)
            .HasForeignKey(t => t.AssignedEmployeeId)
            // SetNull: when someone leaves, their tasks return to the backlog
            // unassigned rather than being deleted with them.
            .OnDelete(DeleteBehavior.SetNull);

        // INDEX: (ProjectId, Status).
        // Composite, and the order is deliberate. Tasks are almost always listed
        // for one project and then filtered by status, so ProjectId leads as the
        // most selective column. A btree can use any leading prefix, so this one
        // index also serves plain "tasks in this project" queries — which is why
        // there is no separate ProjectId index.
        builder.HasIndex(t => new { t.ProjectId, t.Status })
            .HasDatabaseName("ix_tasks_project_id_status");

        // INDEX: (AssignedEmployeeId, Status).
        // Backs the EMPLOYEE role's "my tasks" view, which is the single most
        // frequent query an employee makes, usually narrowed by status.
        builder.HasIndex(t => new { t.AssignedEmployeeId, t.Status })
            .HasDatabaseName("ix_tasks_assigned_employee_id_status");

        // INDEX: DueDate, excluding rows without one.
        // Serves the dashboard's overdue count. Filtered because a task with no
        // due date can never be overdue, so indexing those rows would only make
        // the index larger and slower for no benefit.
        builder.HasIndex(t => t.DueDate)
            .HasFilter("due_date IS NOT NULL")
            .HasDatabaseName("ix_tasks_due_date");

        builder.Property(t => t.CreatedAt).IsRequired();
    }
}
