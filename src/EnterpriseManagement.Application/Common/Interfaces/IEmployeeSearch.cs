using EnterpriseManagement.Domain.Entities;

namespace EnterpriseManagement.Application.Common.Interfaces;

/// <summary>
/// Applies a free-text employee search to a query, using whatever the
/// configured database does best.
/// </summary>
/// <remarks>
/// <para>
/// This interface exists because of a boundary the compiler enforced earlier.
/// <c>EF.Functions.ILike</c> and trigram operators live in the Npgsql provider
/// package; referencing it from Application would tie business logic to
/// PostgreSQL. Declaring the capability here and implementing it in
/// Infrastructure keeps the dependency pointing inward.
/// </para>
/// <para>
/// It takes and returns <see cref="IQueryable{T}"/> rather than a list, so the
/// caller can still append filtering, sorting and paging and have the whole
/// thing execute as one statement. An implementation that materialised results
/// would defeat the purpose.
/// </para>
/// </remarks>
public interface IEmployeeSearch
{
    /// <param name="query">The query to narrow.</param>
    /// <param name="searchTerm">Raw user input. The implementation is responsible for making it safe.</param>
    IQueryable<Employee> ApplySearch(IQueryable<Employee> query, string searchTerm);
}
