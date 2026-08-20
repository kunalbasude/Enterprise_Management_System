using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Application.Common.Interfaces;

/// <summary>
/// Records security- and business-significant actions to the audit trail.
/// </summary>
/// <remarks>
/// <para>
/// Called explicitly by services rather than derived from EF Core change
/// tracking. An interceptor over <c>SaveChanges</c> would be automatic and
/// impossible to forget, but it only sees entity state: it cannot tell a login
/// from a failed login (neither changes an entity), and a task moving to Done
/// looks identical to any other column update. The events worth auditing are
/// business events, and only the caller knows which one occurred.
/// </para>
/// <para>
/// The cost of that choice is discipline — a missing call is a missing audit
/// row. Calls therefore live in services, not controllers, so every path that
/// performs the action is covered.
/// </para>
/// <para>
/// Implementations must never persist credentials. See
/// <c>AuditMetadataSanitiser</c>, which strips them structurally rather than
/// relying on callers to remember.
/// </para>
/// </remarks>
public interface IAuditService
{
    /// <summary>
    /// Writes one audit entry.
    /// </summary>
    /// <param name="action">What happened, in business terms.</param>
    /// <param name="entityType">The type acted upon, e.g. "Project". Free text, so audit rows outlive schema changes.</param>
    /// <param name="entityId">Primary key of the affected row, when the action targets one.</param>
    /// <param name="metadata">
    /// Optional detail, serialised to jsonb. Sanitised before storage; keys that
    /// look like credentials are redacted.
    /// </param>
    /// <param name="userIdOverride">
    /// Actor id when it cannot be read from the current request — the failed
    /// login path, where nobody is authenticated yet.
    /// </param>
    /// <param name="userEmailOverride">Actor email for the same case.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LogAsync(
        AuditAction action,
        string entityType,
        int? entityId = null,
        object? metadata = null,
        int? userIdOverride = null,
        string? userEmailOverride = null,
        CancellationToken cancellationToken = default);
}
