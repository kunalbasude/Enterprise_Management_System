using EnterpriseManagement.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseManagement.Application.Common.Extensions;

/// <summary>
/// Query composition helpers shared by every list endpoint.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Executes a query as a single page, returning the rows plus the total count.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two round-trips by design: one COUNT over the filtered set, one SELECT for
    /// the page. The alternative — fetching everything and counting in memory —
    /// is what turns a 200ms endpoint into a 30-second one once the table grows,
    /// and it is the single most common pagination mistake.
    /// </para>
    /// <para>
    /// The count runs first and short-circuits: if nothing matches, the second
    /// query is skipped entirely.
    /// </para>
    /// <para>
    /// Call this on a query that has already been projected with
    /// <c>Select</c>, so only the DTO's columns cross the wire.
    /// </para>
    /// </remarks>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return new PagedResult<T>([], page, pageSize, 0);
        }

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, page, pageSize, totalCount);
    }

    /// <summary>
    /// Applies a sort chosen from a whitelist, falling back to a default when the
    /// requested field is unknown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whitelist is the security control. The tempting alternative is to
    /// build an expression from the raw <c>sortBy</c> string, or worse to
    /// concatenate it into SQL — which is a direct injection vector. Here an
    /// unrecognised field simply falls back to the default; no caller-supplied
    /// string ever reaches the database as an identifier.
    /// </para>
    /// <para>
    /// Every sort ends with a tiebreaker on a unique column. Without one,
    /// PostgreSQL may return rows with equal sort keys in a different order per
    /// query, so the same row can appear on page 1 and page 2, or on neither.
    /// </para>
    /// </remarks>
    /// <param name="query">The query to sort.</param>
    /// <param name="sortBy">Caller-supplied field name. Case-insensitive.</param>
    /// <param name="descending">Sort direction.</param>
    /// <param name="allowedSorts">Field name to key selector. The whitelist.</param>
    /// <param name="defaultSort">Applied when <paramref name="sortBy"/> is missing or unknown.</param>
    /// <param name="tiebreaker">Unique column ensuring a stable total order.</param>
    public static IQueryable<T> ApplySorting<T>(
        this IQueryable<T> query,
        string? sortBy,
        bool descending,
        IReadOnlyDictionary<string, System.Linq.Expressions.Expression<Func<T, object>>> allowedSorts,
        System.Linq.Expressions.Expression<Func<T, object>> defaultSort,
        System.Linq.Expressions.Expression<Func<T, object>> tiebreaker)
    {
        var selector = defaultSort;

        if (!string.IsNullOrWhiteSpace(sortBy) &&
            allowedSorts.TryGetValue(sortBy.Trim(), out var whitelisted))
        {
            selector = whitelisted;
        }

        var ordered = descending
            ? query.OrderByDescending(selector)
            : query.OrderBy(selector);

        return ordered.ThenBy(tiebreaker);
    }
}
