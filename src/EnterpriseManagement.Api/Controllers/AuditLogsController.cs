using System.Net.Mime;
using EnterpriseManagement.Api.Authorization;
using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.AuditLogs.Dtos;
using EnterpriseManagement.Application.Features.AuditLogs.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseManagement.Api.Controllers;

/// <summary>
/// The audit trail.
/// </summary>
/// <remarks>
/// <para>
/// Read-only and ADMIN-only. There is deliberately no POST, PUT, PATCH or
/// DELETE: an audit trail that can be edited is not evidence of anything. Rows
/// are written only by services, as a side effect of the action they describe.
/// </para>
/// <para>
/// ADMIN-only because the trail aggregates who did what, from which IP, and
/// when — a richer picture of colleagues' activity than any individual endpoint
/// exposes.
/// </para>
/// </remarks>
[ApiController]
[Route("api/audit-logs")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogQueryService _auditLogQueryService;

    public AuditLogsController(IAuditLogQueryService auditLogQueryService)
    {
        _auditLogQueryService = auditLogQueryService;
    }

    /// <summary>Lists audit entries, newest first.</summary>
    /// <remarks>
    /// Examples:
    /// <list type="bullet">
    /// <item><c>GET /api/audit-logs?userId=2</c> — everything one account did</item>
    /// <item><c>GET /api/audit-logs?action=1</c> — failed logins, for spotting credential stuffing</item>
    /// <item><c>GET /api/audit-logs?entityType=TaskItem&amp;entityId=5</c> — the history of one task</item>
    /// <item><c>GET /api/audit-logs?from=2026-08-01T00:00:00Z&amp;to=2026-08-31T23:59:59Z</c></item>
    /// </list>
    /// <para>
    /// Actions: 0 Login, 1 LoginFailed, 2 Logout, 3 Created, 4 Updated,
    /// 5 Deleted, 6 StatusChanged, 7 Assigned, 8 Unassigned.
    /// </para>
    /// </remarks>
    /// <response code="200">A page of audit entries.</response>
    /// <response code="400">Invalid filter or sort.</response>
    /// <response code="403">Authenticated but not an ADMIN.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetAll(
        [FromQuery] AuditLogQueryParameters parameters,
        CancellationToken cancellationToken) =>
        Ok(await _auditLogQueryService.GetAsync(parameters, cancellationToken));
}
