using EnterpriseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseManagement.Application.Common.Interfaces;

/// <summary>
/// The database surface the Application layer is allowed to see.
/// </summary>
/// <remarks>
/// <para>
/// Application declares this interface and Infrastructure's <c>AppDbContext</c>
/// implements it. That inverts the dependency: the policy layer owns the
/// contract, and the persistence detail conforms to it.
/// </para>
/// <para>
/// <b>Why this returns <see cref="DbSet{TEntity}"/> rather than a hand-rolled
/// abstraction:</b> <c>DbSet</c> is an <c>IQueryable</c>, so filtering, sorting
/// and paging compose into a single SQL statement executed by the database. A
/// repository returning <c>List&lt;T&gt;</c> or <c>IEnumerable&lt;T&gt;</c> would
/// force every row into memory before paging — the exact anti-pattern this
/// project is meant to avoid.
/// </para>
/// <para>
/// <b>The trade-off, stated honestly:</b> this couples Application to the EF Core
/// abstractions package. It does not couple it to PostgreSQL, to Npgsql, or to
/// any migration. Swapping providers touches Infrastructure only. Hiding
/// <c>IQueryable</c> entirely would mean reinventing LINQ, and the coupling would
/// still exist through a worse API.
/// </para>
/// </remarks>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Department> Departments { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectEmployee> ProjectEmployees { get; }
    DbSet<TaskItem> Tasks { get; }
    DbSet<AuditLog> AuditLogs { get; }

    /// <summary>
    /// Commits all tracked changes in a single transaction.
    /// </summary>
    /// <remarks>
    /// The <see cref="CancellationToken"/> is not decoration: when a client
    /// disconnects mid-request ASP.NET Core signals it, and passing it down lets
    /// the database call be abandoned instead of holding a connection for a
    /// response nobody will read.
    /// </remarks>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
