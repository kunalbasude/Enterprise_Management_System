namespace EnterpriseManagement.Application.Common.Models;

/// <summary>
/// One page of results plus the metadata a client needs to render pagination.
/// </summary>
/// <remarks>
/// Collections are wrapped because the metadata has nowhere else to live.
/// Single resources are returned bare — wrapping every 200 in
/// <c>{ success: true, data: ... }</c> duplicates what the status code already
/// says and forces every client to unwrap twice.
/// </remarks>
/// <typeparam name="T">The DTO being paged. Never an entity.</typeparam>
public class PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> data, int page, int pageSize, int totalCount)
    {
        Data = data;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    public IReadOnlyList<T> Data { get; }

    public int Page { get; }

    public int PageSize { get; }

    /// <summary>Total matching rows, ignoring paging. Requires its own COUNT query.</summary>
    public int TotalCount { get; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;
}
