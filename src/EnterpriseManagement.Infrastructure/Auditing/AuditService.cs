using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Domain.Entities;
using EnterpriseManagement.Domain.Enums;
using EnterpriseManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace EnterpriseManagement.Infrastructure.Auditing;

/// <summary>
/// Writes audit entries to the database.
/// </summary>
/// <remarks>
/// <para>
/// Lives in Infrastructure because it persists. Application depends only on
/// <see cref="IAuditService"/>.
/// </para>
/// <para>
/// <b>Failure policy.</b> An audit write that fails is logged and swallowed
/// rather than propagated. The alternative — letting it fail the request — means
/// a transient database hiccup during the audit insert would prevent someone
/// logging in, or roll back a task update that already succeeded. For a
/// regulated system the opposite choice is correct, and there the answer is a
/// transactional outbox rather than a shared transaction. The trade-off is
/// deliberate and the failure is loud in the application log.
/// </para>
/// </remarks>
public class AuditService : IAuditService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IRequestContext _requestContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        AppDbContext context,
        ICurrentUser currentUser,
        IRequestContext requestContext,
        TimeProvider timeProvider,
        ILogger<AuditService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _requestContext = requestContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task LogAsync(
        AuditAction action,
        string entityType,
        int? entityId = null,
        object? metadata = null,
        int? userIdOverride = null,
        string? userEmailOverride = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = new AuditLog
            {
                // The override exists for the failed-login path, where nobody is
                // authenticated yet but the attempt still matters.
                UserId = userIdOverride ?? _currentUser.UserId,

                // Denormalised on purpose: the trail must stay readable after the
                // user is renamed or deleted, and listing it needs no join.
                UserEmail = userEmailOverride ?? _currentUser.Email,

                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Metadata = AuditMetadataSanitiser.Sanitise(metadata),
                IpAddress = _requestContext.IpAddress,
                CorrelationId = _requestContext.CorrelationId,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            _context.AuditLogs.Add(entry);

            // Its own SaveChanges, after the caller's. A crash between the two
            // loses the audit row but keeps the business change; the reverse
            // policy would let an audit failure block a login.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to write audit entry {Action} for {EntityType} {EntityId}",
                action, entityType, entityId);
        }
    }
}
