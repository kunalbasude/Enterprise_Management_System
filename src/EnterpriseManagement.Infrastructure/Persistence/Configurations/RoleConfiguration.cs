using EnterpriseManagement.Domain.Common;
using EnterpriseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseManagement.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(50);

        // INDEX: unique on Name.
        // Role names are a security contract: they are written into JWT role
        // claims and matched by [Authorize(Roles = ...)]. Two rows named "ADMIN"
        // would make authorisation results depend on which row a query happened
        // to return.
        builder.HasIndex(r => r.Name)
            .IsUnique()
            .HasDatabaseName("ix_roles_name");

        builder.Property(r => r.Description)
            .IsRequired()
            .HasMaxLength(200);

        // Seeded here rather than at runtime: these three rows are part of the
        // schema's meaning, so they belong in the migration. Explicit ids are
        // required because HasData runs without a database round-trip.
        builder.HasData(
            new Role { Id = 1, Name = RoleNames.Admin, Description = "Full system access." },
            new Role { Id = 2, Name = RoleNames.Manager, Description = "Manages own projects, tasks and team." },
            new Role { Id = 3, Name = RoleNames.Employee, Description = "Views own profile, projects and tasks." });
    }
}
