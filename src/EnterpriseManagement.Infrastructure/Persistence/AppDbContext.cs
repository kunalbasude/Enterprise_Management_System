using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Domain.Common;
using EnterpriseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseManagement.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit of work and change tracker for this application.
/// </summary>
/// <remarks>
/// Registered with a <b>scoped</b> lifetime: one instance per HTTP request.
/// A <see cref="DbContext"/> is not thread-safe and accumulates tracked entities,
/// so a singleton would both corrupt under concurrency and leak memory.
/// </remarks>
public class AppDbContext : DbContext, IApplicationDbContext
{
    private readonly TimeProvider _timeProvider;

    /// <param name="options">Provider, connection string and naming conventions, supplied by DI.</param>
    /// <param name="timeProvider">
    /// Injected rather than calling <c>DateTime.UtcNow</c> directly so that tests
    /// can control the clock. This is the .NET 8 built-in abstraction; there is
    /// no need for a hand-written IDateTime interface.
    /// </param>
    public AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider)
        : base(options)
    {
        _timeProvider = timeProvider;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectEmployee> ProjectEmployees => Set<ProjectEmployee>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Discovers every IEntityTypeConfiguration<T> in this assembly. Keeps the
        // mapping for each entity in its own file instead of one thousand-line
        // OnModelCreating, and means adding an entity needs no edit here.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    /// <summary>
    /// Stamps audit timestamps centrally, then delegates to EF Core.
    /// </summary>
    /// <remarks>
    /// Doing this here rather than in each service is the difference between a
    /// rule that holds and a rule that holds until someone forgets. The change
    /// tracker already knows exactly which entities are Added or Modified, so
    /// this costs one pass over entries that are being written anyway.
    /// </remarks>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditTimestamps()
    {
        // UTC everywhere. Npgsql maps DateTime to timestamptz and throws on any
        // value whose Kind is not Utc, so a local time here fails loudly at the
        // insert rather than silently storing a wrong instant.
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = null;
                    break;

                case EntityState.Modified:
                    // Guard against a client-supplied CreatedAt overwriting the
                    // original value on update.
                    entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
