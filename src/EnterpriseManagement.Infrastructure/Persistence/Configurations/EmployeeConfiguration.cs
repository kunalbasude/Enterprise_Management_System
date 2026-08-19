using EnterpriseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseManagement.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EmployeeCode)
            .IsRequired()
            .HasMaxLength(20);

        // INDEX: unique on EmployeeCode.
        // The business key people actually quote. Unique because two staff sharing
        // a code makes every downstream report ambiguous, and indexed because it
        // is looked up directly.
        builder.HasIndex(e => e.EmployeeCode)
            .IsUnique()
            .HasDatabaseName("ix_employees_employee_code");

        builder.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.LastName).IsRequired().HasMaxLength(100);

        builder.Property(e => e.Email).IsRequired().HasMaxLength(256);

        // INDEX: unique on Email.
        // One work address per person. Also serves exact-match lookups; the
        // partial-match search added in Phase 8 needs a trigram index instead,
        // because a btree cannot serve a leading-wildcard LIKE.
        builder.HasIndex(e => e.Email)
            .IsUnique()
            .HasDatabaseName("ix_employees_email");

        builder.Property(e => e.PhoneNumber).HasMaxLength(30);
        builder.Property(e => e.JobTitle).IsRequired().HasMaxLength(100);
        builder.Property(e => e.HireDate).IsRequired();
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

        // FullName is a computed C# property with no setter. EF would otherwise
        // try to map it to a column that does not exist.
        builder.Ignore(e => e.FullName);

        builder.HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId)
            // Restrict: deleting a department that still has staff must fail with
            // a foreign key error. Cascade here would silently delete people.
            .OnDelete(DeleteBehavior.Restrict);

        // INDEX: DepartmentId.
        // Serves ?departmentId= filtering, which is a primary employee filter, and
        // makes the Restrict check above an index lookup rather than a table scan.
        builder.HasIndex(e => e.DepartmentId)
            .HasDatabaseName("ix_employees_department_id");

        // One employee record per login, and the link is optional on both sides.
        builder.HasOne(e => e.User)
            .WithOne(u => u.Employee)
            .HasForeignKey<Employee>(e => e.UserId)
            // SetNull: disabling or removing a login must not delete the HR
            // record, which tasks and audit rows still reference.
            .OnDelete(DeleteBehavior.SetNull);

        // INDEX: unique on UserId, filtered to non-null rows.
        // A plain unique index would treat multiple NULLs as distinct in
        // PostgreSQL, which is correct here — but stating the filter explicitly
        // documents the intent and keeps the index smaller when most employees
        // have no account.
        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasFilter("user_id IS NOT NULL")
            .HasDatabaseName("ix_employees_user_id");

        // INDEX: (LastName, FirstName).
        // The default sort order of the employee list. Without it every paged
        // request sorts the whole table; with it PostgreSQL walks the index in
        // order and stops after pageSize rows.
        builder.HasIndex(e => new { e.LastName, e.FirstName })
            .HasDatabaseName("ix_employees_last_name_first_name");

        builder.Property(e => e.CreatedAt).IsRequired();
    }
}
