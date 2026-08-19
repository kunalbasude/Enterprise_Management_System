using EnterpriseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseManagement.Infrastructure.Persistence.Configurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");

        // Composite primary key. This is both the identity of the row and the
        // uniqueness rule "a user holds a role at most once" — no extra unique
        // index needed. PostgreSQL backs it with a btree on (UserId, RoleId),
        // which also serves "which roles does this user have?" since UserId
        // leads. The reverse question needs the explicit index below.
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            // Removing a user should remove their role assignments: the join row
            // is meaningless without both sides. This is one of the few places
            // where cascade is genuinely correct.
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            // Restrict, not Cascade: deleting the ADMIN role must fail loudly
            // while anyone still holds it, rather than silently stripping
            // permissions from live accounts.
            .OnDelete(DeleteBehavior.Restrict);

        // INDEX: RoleId.
        // The composite PK leads with UserId, so it cannot answer "who are all
        // the admins?" efficiently. This index makes that scan an index lookup.
        builder.HasIndex(ur => ur.RoleId)
            .HasDatabaseName("ix_user_roles_role_id");

        builder.Property(ur => ur.AssignedAt).IsRequired();
    }
}
