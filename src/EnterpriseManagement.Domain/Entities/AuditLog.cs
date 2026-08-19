using EnterpriseManagement.Domain.Common;
using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Domain.Entities;

/// <summary>
/// An append-only record of a security- or business-significant action.
/// </summary>
/// <remarks>
/// <para>
/// Rows are never updated or deleted; that is the entire value of an audit trail.
/// It therefore does not implement <see cref="IAuditableEntity"/> — an
/// <c>UpdatedAt</c> on an immutable row would be meaningless.
/// </para>
/// <para>
/// <b>Never written here:</b> passwords, password hashes, JWTs, or any
/// <c>Authorization</c> header. An audit log is widely readable by design, which
/// makes it the worst possible place to leak a credential.
/// </para>
/// </remarks>
public class AuditLog : BaseEntity
{
    /// <summary>
    /// Who acted. Null for anonymous events such as a failed login against an
    /// address that does not exist — the event still matters for detecting
    /// credential stuffing.
    /// </summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>
    /// Denormalised copy of the actor's email at the time of the action. Kept so
    /// the trail stays readable even if the user row is later renamed, and so
    /// listing the log needs no join.
    /// </summary>
    public string? UserEmail { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>The entity type acted upon, e.g. "Project". Plain string, not a FK: audit rows must outlive schema changes.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Primary key of the affected row, when the action targets one.</summary>
    public int? EntityId { get; set; }

    /// <summary>
    /// Optional JSON detail, e.g. which fields changed. Stored as jsonb in
    /// PostgreSQL so it stays queryable rather than being an opaque blob.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Caller IP. Personal data under GDPR, so it is recorded because it is
    /// genuinely needed to investigate account compromise, and nothing else.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>Correlates this row with the application log lines for the same request.</summary>
    public string? CorrelationId { get; set; }

    public DateTime CreatedAt { get; set; }
}
