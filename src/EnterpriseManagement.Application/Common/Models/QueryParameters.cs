namespace EnterpriseManagement.Application.Common.Models;

/// <summary>
/// Base for every list endpoint's query string: paging, search and sorting.
/// </summary>
/// <remarks>
/// Feature-specific parameter classes inherit from this and add their own
/// filters, so paging behaves identically everywhere and the defaults live in
/// exactly one place.
/// </remarks>
public class QueryParameters
{
    /// <summary>
    /// Hard ceiling on page size. Without it a caller can request
    /// <c>?pageSize=1000000</c> and turn a paged endpoint into a full table
    /// dump — a denial-of-service vector that costs nothing to close.
    /// </summary>
    public const int MaxPageSize = 100;

    public const int DefaultPageSize = 20;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    /// <summary>1-based page number. Values below 1 are clamped rather than rejected.</summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Clamped into [1, <see cref="MaxPageSize"/>] on assignment, so no query can bypass it.</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    /// <summary>Free-text search term. Which fields it matches is per-endpoint.</summary>
    public string? Search { get; set; }

    /// <summary>
    /// Field to sort by. Validated against a per-endpoint whitelist before use —
    /// never concatenated into SQL or passed to a dynamic LINQ evaluator.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>"asc" or "desc". Anything else is treated as ascending.</summary>
    public string? SortOrder { get; set; }

    public bool IsDescending =>
        string.Equals(SortOrder, "desc", StringComparison.OrdinalIgnoreCase);

    /// <summary>Rows to skip. Derived, so no caller can pass an inconsistent offset.</summary>
    public int Skip => (Page - 1) * PageSize;
}
