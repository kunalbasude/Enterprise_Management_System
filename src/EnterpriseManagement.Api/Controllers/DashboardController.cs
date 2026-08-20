using System.Net.Mime;
using EnterpriseManagement.Api.Authorization;
using EnterpriseManagement.Application.Features.Dashboard.Dtos;
using EnterpriseManagement.Application.Features.Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseManagement.Api.Controllers;

/// <summary>Aggregate statistics for the landing page.</summary>
[ApiController]
[Route("api/dashboard")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>Returns headline figures scoped to the caller.</summary>
    /// <remarks>
    /// <para>
    /// Open to every authenticated user, because the figures are scoped rather
    /// than filtered afterwards. An ADMIN sees the organisation; anyone else
    /// sees only the projects they manage or belong to, plus tasks assigned to
    /// them personally. The <c>scope</c> field states which, so a client can
    /// label the numbers honestly instead of implying they are company-wide.
    /// </para>
    /// <para>
    /// Computed in a single database round-trip. The obvious implementation —
    /// one CountAsync per figure — costs a round-trip each, which is invisible
    /// locally and adds up to real latency on the first screen every user loads.
    /// </para>
    /// </remarks>
    /// <response code="200">The summary.</response>
    /// <response code="401">No or invalid token.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken cancellationToken) =>
        Ok(await _dashboardService.GetSummaryAsync(cancellationToken));
}
