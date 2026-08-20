using EnterpriseManagement.Infrastructure.Search;

namespace EnterpriseManagement.Tests.Employees;

/// <summary>
/// Covers LIKE wildcard escaping in employee search.
/// </summary>
/// <remarks>
/// A correctness control, not an injection defence — the term is always passed
/// as a parameter, so injection is already impossible. Without escaping, a
/// search for "%" matches every row (turning a cheap query into a full scan
/// returning the entire directory) and "_" silently matches any single
/// character, quietly returning wrong results.
/// </remarks>
public class EmployeeSearchEscapingTests
{
    [Fact]
    public void Percent_is_escaped_so_it_matches_literally()
    {
        Assert.Equal("100\\%", PostgresEmployeeSearch.EscapeLikeWildcards("100%"));
    }

    [Fact]
    public void Underscore_is_escaped()
    {
        Assert.Equal("first\\_name", PostgresEmployeeSearch.EscapeLikeWildcards("first_name"));
    }

    [Fact]
    public void Backslash_is_escaped_first_so_escapes_are_not_applied_twice()
    {
        // Order matters. Escaping % before \ would turn "\%" into "\\%", which
        // means "literal backslash followed by a wildcard" rather than
        // "literal percent sign".
        Assert.Equal("a\\\\b", PostgresEmployeeSearch.EscapeLikeWildcards(@"a\b"));
    }

    [Fact]
    public void A_wildcard_only_search_cannot_match_everything()
    {
        var escaped = PostgresEmployeeSearch.EscapeLikeWildcards("%");

        Assert.Equal("\\%", escaped);
        Assert.NotEqual("%", escaped);
    }

    [Theory]
    [InlineData("john")]
    [InlineData("GEN-000123")]
    [InlineData("o'brien")]
    [InlineData("jean-luc")]
    [InlineData("user@example.com")]
    public void Ordinary_terms_pass_through_unchanged(string term)
    {
        Assert.Equal(term, PostgresEmployeeSearch.EscapeLikeWildcards(term));
    }
}
