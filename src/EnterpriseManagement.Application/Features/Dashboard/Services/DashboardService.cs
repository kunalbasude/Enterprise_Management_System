using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Application.Features.Dashboard.Dtos;
using EnterpriseManagement.Domain.Common;

namespace EnterpriseManagement.Application.Features.Dashboard.Services;

/// <summary>
/// Resolves who is asking, then delegates the arithmetic to the statistics
/// provider.
/// </summary>
/// <remarks>
/// Thin on purpose. The scoping decision is a business rule and belongs here;
/// how the counts are computed is a persistence detail and belongs behind
/// <see cref="IDashboardStatisticsProvider"/>.
/// </remarks>
public class DashboardService : IDashboardService
{
    private readonly IDashboardStatisticsProvider _statistics;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public DashboardService(
        IDashboardStatisticsProvider statistics,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _statistics = statistics;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var isAdmin = _currentUser.IsInRole(RoleNames.Admin);

        // Today in UTC. A user in another timezone may see a task flip to
        // overdue slightly early or late; correcting that means storing each
        // user's timezone, which is a real feature rather than a rounding fix.
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        return _statistics.GetSummaryAsync(isAdmin, _currentUser.EmployeeId, today, cancellationToken);
    }
}
