using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Application.Features.AuditLogs.Dtos;

public class AuditLogQueryParameters : QueryParameters
{
    /// <summary>Filter to one actor. Served by ix_audit_logs_user_id_created_at.</summary>
    public int? UserId { get; set; }

    public AuditAction? Action { get; set; }

    /// <summary>Filter by entity type, e.g. "Project".</summary>
    public string? EntityType { get; set; }

    public int? EntityId { get; set; }

    /// <summary>Inclusive lower bound on CreatedAt (UTC).</summary>
    public DateTime? From { get; set; }

    /// <summary>Inclusive upper bound on CreatedAt (UTC).</summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// Only CreatedAt is sortable.
    /// </summary>
    /// <remarks>
    /// Deliberately narrow. An audit trail is read chronologically, and
    /// ix_audit_logs_created_at is descending so the default view walks the
    /// index rather than sorting the table. Offering more sort fields would
    /// invite full sorts over the largest table in the system for no real gain.
    /// </remarks>
    public static readonly IReadOnlyList<string> SortableFields = ["createdAt"];
}
