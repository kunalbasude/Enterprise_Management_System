using EnterpriseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseManagement.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);

        builder.Property(p => p.Code).IsRequired().HasMaxLength(30);

        // INDEX: unique on Code. The human-facing project key.
        builder.HasIndex(p => p.Code)
            .IsUnique()
            .HasDatabaseName("ix_projects_code");

        builder.Property(p => p.Description).IsRequired().HasMaxLength(2000);

        // Enum stored as int, which is EF's default.
        // Alternative: HasConversion<string>() for a human-readable column.
        // int is chosen because the column is filtered and indexed constantly and
        // 4 bytes beats a varchar in both index size and comparison cost. The
        // cost is that raw SQL shows "1" instead of "Active", and that reordering
        // the enum members would silently reinterpret existing rows — which is
        // why every enum in this project has explicit numeric values.
        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<int>();

        // INDEX: Status.
        // Serves ?status=ACTIVE filtering and the dashboard's active-project
        // count. Low cardinality (five values), so PostgreSQL will still prefer a
        // sequential scan when a status matches most of the table — that is
        // correct behaviour, not a missing index.
        builder.HasIndex(p => p.Status)
            .HasDatabaseName("ix_projects_status");

        builder.Property(p => p.StartDate).IsRequired();

        builder.HasOne(p => p.Manager)
            .WithMany(e => e.ManagedProjects)
            .HasForeignKey(p => p.ManagerEmployeeId)
            // Restrict: an employee who still manages a project cannot be deleted.
            // Reassignment must be a deliberate act, not a side effect.
            .OnDelete(DeleteBehavior.Restrict);

        // INDEX: ManagerEmployeeId.
        // The MANAGER role's authorisation check resolves to "is this project
        // mine?", and their project list filters on it. This runs on nearly every
        // manager request, so it is one of the highest-value indexes here.
        builder.HasIndex(p => p.ManagerEmployeeId)
            .HasDatabaseName("ix_projects_manager_employee_id");

        builder.Property(p => p.CreatedAt).IsRequired();
    }
}
