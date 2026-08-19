using EnterpriseManagement.Application.Common.Models;

namespace EnterpriseManagement.Tests.Common;

public class QueryParametersTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void Page_is_clamped_to_at_least_one(int requested, int expected)
    {
        var parameters = new QueryParameters { Page = requested };

        Assert.Equal(expected, parameters.Page);
    }

    [Theory]
    [InlineData(0, QueryParameters.DefaultPageSize)]
    [InlineData(-1, QueryParameters.DefaultPageSize)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    // The security-relevant case: ?pageSize=1000000 must not dump the table.
    [InlineData(1_000_000, QueryParameters.MaxPageSize)]
    public void PageSize_is_clamped_to_the_maximum(int requested, int expected)
    {
        var parameters = new QueryParameters { PageSize = requested };

        Assert.Equal(expected, parameters.PageSize);
    }

    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(2, 20, 20)]
    [InlineData(5, 10, 40)]
    public void Skip_is_derived_from_page_and_size(int page, int size, int expectedSkip)
    {
        var parameters = new QueryParameters { Page = page, PageSize = size };

        Assert.Equal(expectedSkip, parameters.Skip);
    }

    [Theory]
    [InlineData("desc", true)]
    [InlineData("DESC", true)]
    [InlineData("asc", false)]
    [InlineData(null, false)]
    [InlineData("nonsense", false)]
    public void Sort_order_defaults_to_ascending_for_anything_but_desc(string? order, bool expected)
    {
        var parameters = new QueryParameters { SortOrder = order };

        Assert.Equal(expected, parameters.IsDescending);
    }
}

public class PagedResultTests
{
    [Theory]
    [InlineData(150, 20, 8)]   // partial last page rounds up
    [InlineData(100, 20, 5)]   // exact division
    [InlineData(0, 20, 0)]     // empty result set
    [InlineData(1, 20, 1)]
    public void TotalPages_rounds_up(int totalCount, int pageSize, int expected)
    {
        var result = new PagedResult<string>([], 1, pageSize, totalCount);

        Assert.Equal(expected, result.TotalPages);
    }

    [Fact]
    public void Navigation_flags_are_correct_on_a_middle_page()
    {
        var result = new PagedResult<string>([], page: 3, pageSize: 20, totalCount: 150);

        Assert.True(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public void Last_page_has_no_next()
    {
        var result = new PagedResult<string>([], page: 8, pageSize: 20, totalCount: 150);

        Assert.True(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void Empty_result_set_has_no_navigation()
    {
        var result = new PagedResult<string>([], page: 1, pageSize: 20, totalCount: 0);

        Assert.False(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }
}
