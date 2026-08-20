using EnterpriseManagement.Application.Features.Dashboard.Dtos;

namespace EnterpriseManagement.Tests.Dashboard;

public class DashboardSummaryDtoTests
{
    [Theory]
    [InlineData(0, 0, 0)]        // no tasks at all
    [InlineData(10, 0, 0)]       // nothing done yet
    [InlineData(10, 10, 100)]    // everything done
    [InlineData(10, 4, 40)]
    [InlineData(3, 1, 33.3)]     // rounds to one decimal place
    [InlineData(7, 2, 28.6)]
    public void Completion_rate_is_derived_from_the_counts(int total, int completed, decimal expected)
    {
        var summary = new DashboardSummaryDto { TotalTasks = total, CompletedTasks = completed };

        Assert.Equal(expected, summary.CompletionRate);
    }

    [Fact]
    public void Completion_rate_is_zero_rather_than_a_divide_by_zero()
    {
        // A brand-new deployment has no tasks. The dashboard is the first screen
        // loaded, so this is the very first request the system ever serves.
        var summary = new DashboardSummaryDto { TotalTasks = 0, CompletedTasks = 0 };

        Assert.Equal(0, summary.CompletionRate);
    }

    [Fact]
    public void Completion_rate_uses_decimal_not_integer_division()
    {
        // With int division 1/3 is 0, so a third of the work would report as 0%.
        var summary = new DashboardSummaryDto { TotalTasks = 3, CompletedTasks = 1 };

        Assert.True(summary.CompletionRate > 33m);
        Assert.True(summary.CompletionRate < 34m);
    }

    [Fact]
    public void Pending_and_completed_need_not_sum_to_total()
    {
        // Cancelled tasks are neither completed nor pending. Asserting the sum
        // would encode a rule the domain deliberately does not have.
        var summary = new DashboardSummaryDto
        {
            TotalTasks = 10,
            CompletedTasks = 4,
            PendingTasks = 4   // the other 2 are Cancelled
        };

        Assert.Equal(40m, summary.CompletionRate);
        Assert.NotEqual(summary.TotalTasks, summary.CompletedTasks + summary.PendingTasks);
    }
}
