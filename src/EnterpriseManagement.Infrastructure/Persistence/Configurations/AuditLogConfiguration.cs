using EnterpriseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseManagement.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).IsRequired().HasConversion<int>();

        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.UserEmail).HasMaxLength(256);
        builder.Property(a => a.IpAddress).HasMaxLength(45);   // 45 = max INET6 text length
        builder.Property(a => a.CorrelationId).HasMaxLength(64);

        // jsonb, not text. PostgreSQL parses and stores it in a binary form that
        // stays queryable (metadata->>'field'), which matters when investigating
        // an incident. The cost is a parse on write and a stricter format.
        builder.Property(a => a.Metadata).HasColumnType("jsonb");

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            // SetNull: an audit row must outlive the user it describes. Cascade
            // here would let deleting an account erase the evidence of what it
            // did, which defeats the purpose of an audit trail.
            .OnDelete(DeleteBehavior.SetNull);

        // INDEX: CreatedAt descending.
        // The audit log is read newest-first and paged. Matching the index order
        // to the query's ORDER BY lets PostgreSQL walk it backwards and stop
        // after pageSize rows instead of sorting the whole table.
        builder.HasIndex(a => a.CreatedAt)
            .IsDescending()
            .HasDatabaseName("ix_audit_logs_created_at");

        // INDEX: (UserId, CreatedAt DESC).
        // "What did this account do?" is the first question in any incident.
        builder.HasIndex(a => new { a.UserId, a.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_audit_logs_user_id_created_at");

        builder.Property(a => a.CreatedAt).IsRequired();
    }
}
