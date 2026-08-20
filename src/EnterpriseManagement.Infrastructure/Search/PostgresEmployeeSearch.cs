using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseManagement.Infrastructure.Search;

/// <summary>
/// PostgreSQL employee search using case-insensitive ILIKE across the fields
/// people actually search by.
/// </summary>
/// <remarks>
/// <para>
/// <c>ILIKE '%term%'</c> has a leading wildcard, which a B-tree index cannot
/// serve — the planner must scan every row. That is acceptable at small scale
/// and is measured rather than assumed in this project. The fix, applied in the
/// same phase, is a <c>pg_trgm</c> GIN index, which indexes three-character
/// substrings and therefore *can* answer a leading-wildcard match.
/// </para>
/// <para>
/// The term is passed as a parameter, never concatenated into SQL. LIKE
/// wildcards inside user input are escaped so a search for "100%" looks for a
/// literal percent sign rather than matching everything.
/// </para>
/// </remarks>
public class PostgresEmployeeSearch : IEmployeeSearch
{
    public IQueryable<Employee> ApplySearch(IQueryable<Employee> query, string searchTerm)
    {
        var pattern = $"%{EscapeLikeWildcards(searchTerm.Trim())}%";

        return query.Where(e =>
            EF.Functions.ILike(e.FirstName, pattern) ||
            EF.Functions.ILike(e.LastName, pattern) ||
            EF.Functions.ILike(e.Email, pattern) ||
            EF.Functions.ILike(e.EmployeeCode, pattern));
    }

    /// <summary>
    /// Escapes the LIKE metacharacters so they are matched literally.
    /// </summary>
    /// <remarks>
    /// Not an injection defence — parameterisation already handles that. This
    /// is about correctness: without it, a search for "%" matches every row,
    /// and "_" matches any single character, which surprises users and makes
    /// the query far more expensive than intended.
    /// </remarks>
    public static string EscapeLikeWildcards(string input) =>
        input.Replace("\\", "\\\\")
             .Replace("%", "\\%")
             .Replace("_", "\\_");
}
