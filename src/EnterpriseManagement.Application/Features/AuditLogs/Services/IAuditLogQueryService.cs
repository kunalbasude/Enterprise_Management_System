using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.AuditLogs.Dtos;

namespace EnterpriseManagement.Application.Features.AuditLogs.Services;

/// <summary>
/// Reads the audit trail.
/// </summary>
/// <remarks>
/// Separate from <c>IAuditService</c>, which writes it. The split is not
/// ceremony: writing is a cross-cutting concern every service depends on,
/// while reading is a single administrative feature. Keeping them apart means
/// no business service can accidentally acquire the ability to query — or
/// worse, modify — the trail.
/// </remarks>
public interface IAuditLogQueryService
{
    Task<PagedResult<AuditLogDto>> GetAsync(
        AuditLogQueryParameters parameters, CancellationToken cancellationToken = default);
}
