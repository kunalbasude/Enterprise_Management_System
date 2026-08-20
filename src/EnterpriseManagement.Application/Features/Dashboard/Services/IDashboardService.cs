using EnterpriseManagement.Application.Features.Dashboard.Dtos;

namespace EnterpriseManagement.Application.Features.Dashboard.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}
