using EnterpriseManagement.Application.Features.Dashboard.Dtos;

namespace EnterpriseManagement.Application.Common.Interfaces;

/// <summary>
/// Produces the dashboard aggregate in a single database round-trip.
/// </summary>
/// <remarks>
/// <para>
/// One of the few places in this project where a dedicated data-access
/// abstraction is justified. Everywhere else <c>IApplicationDbContext</c> is
/// enough, because those operations are ordinary LINQ over one aggregate root.
/// A dashboard is different: it needs nine conditional counts across four
/// tables, which LINQ expresses awkwardly and, without care, as nine separate
/// statements.
/// </para>
/// <para>
/// Declared here and implemented in Infrastructure with hand-written SQL, so
/// Application asks for statistics without learning where they come from —
/// the same seam as <see cref="IEmployeeSearch"/>.
/// </para>
/// </remarks>
public interface IDashboardStatisticsProvider
{
    /// <param name="isAdmin">When true, figures cover the whole organisation.</param>
    /// <param name="employeeId">
    /// The caller's employee record, used to scope a manager's or employee's
    /// figures. Null for accounts with no employee link, which see zeroes.
    /// </param>
    /// <param name="today">The current date, for the overdue calculation. Passed in so tests can fix it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DashboardSummaryDto> GetSummaryAsync(
        bool isAdmin,
        int? employeeId,
        DateOnly today,
        CancellationToken cancellationToken = default);
}
