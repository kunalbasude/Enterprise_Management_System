using System.Linq.Expressions;
using EnterpriseManagement.Application.Common.Extensions;

namespace EnterpriseManagement.Tests.Common;

/// <summary>
/// Covers the sort whitelist, which is the control that keeps a caller-supplied
/// field name from ever reaching the database as an identifier.
/// </summary>
public class ApplySortingTests
{
    private sealed record Row(int Id, string Name, int Count);

    private static readonly Dictionary<string, Expression<Func<Row, object>>> Allowed =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = r => r.Name,
            ["count"] = r => r.Count
        };

    private static readonly List<Row> Data =
    [
        new(3, "Charlie", 5),
        new(1, "Alpha", 9),
        new(2, "Bravo", 5)
    ];

    private static List<Row> Sort(string? sortBy, bool descending = false) =>
        Data.AsQueryable()
            .ApplySorting(sortBy, descending, Allowed,
                defaultSort: r => r.Name,
                tiebreaker: r => r.Id)
            .ToList();

    [Fact]
    public void Sorts_by_a_whitelisted_field()
    {
        Assert.Equal(["Alpha", "Bravo", "Charlie"], Sort("name").Select(r => r.Name));
    }

    [Fact]
    public void Field_names_are_case_insensitive()
    {
        // Clients should not have to guess casing.
        Assert.Equal(Sort("name").Select(r => r.Id), Sort("NAME").Select(r => r.Id));
    }

    [Fact]
    public void Descending_reverses_the_order()
    {
        Assert.Equal(["Charlie", "Bravo", "Alpha"], Sort("name", descending: true).Select(r => r.Name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknownField")]
    [InlineData("passwordHash")]
    [InlineData("name; DROP TABLE departments;--")]
    [InlineData("(SELECT password_hash FROM users LIMIT 1)")]
    public void Anything_not_whitelisted_falls_back_to_the_default(string? sortBy)
    {
        // The injection strings are never interpreted: they simply fail the
        // dictionary lookup. No caller input becomes part of a query.
        Assert.Equal(["Alpha", "Bravo", "Charlie"], Sort(sortBy).Select(r => r.Name));
    }

    [Fact]
    public void Ties_are_broken_by_the_unique_column_so_paging_is_stable()
    {
        // Charlie and Bravo both have Count 5. Without a tiebreaker their
        // relative order is undefined, so the same row could appear on two
        // pages or on none.
        var byCount = Sort("count").Select(r => r.Id).ToList();

        Assert.Equal([2, 3, 1], byCount);
    }

    [Fact]
    public void Sorting_composes_rather_than_enumerating()
    {
        // The result must still be IQueryable so Skip/Take are appended to the
        // same statement. Materialising here would page in memory.
        var sorted = Data.AsQueryable()
            .ApplySorting("name", false, Allowed, r => r.Name, r => r.Id);

        Assert.IsAssignableFrom<IQueryable<Row>>(sorted);
    }
}
