using System.Linq.Expressions;
using EnterpriseManagement.Application.Common.Extensions;
using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Departments.Dtos;
using EnterpriseManagement.Domain.Entities;
using EnterpriseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnterpriseManagement.Application.Features.Departments.Services;

public class DepartmentService : IDepartmentService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<DepartmentService> _logger;

    /// <summary>
    /// Sort whitelist. A caller-supplied field name is only ever used as a
    /// dictionary key here — it never becomes part of a query string sent to
    /// the database, which is what makes sorting injection-proof.
    /// </summary>
    private static readonly Dictionary<string, Expression<Func<DepartmentDto, object>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = d => d.Name,
            ["createdAt"] = d => d.CreatedAt,
            ["employeeCount"] = d => d.EmployeeCount
        };

    public DepartmentService(IApplicationDbContext context, ILogger<DepartmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<DepartmentDto>> GetAsync(
        DepartmentQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // AsNoTracking: this is a read. Skipping the change tracker avoids
        // snapshotting every row for a comparison that will never happen.
        var query = _context.Departments
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var term = parameters.Search.Trim();

            // Translated by EF Core to `lower(name) LIKE '%term%'` and executed
            // in the database. The equivalent written after a ToList() would
            // pull the whole table into memory first, which is the mistake this
            // project exists to demonstrate avoiding.
            //
            // NOTE: EF.Functions.ILike would be the natural PostgreSQL form, but
            // it lives in the Npgsql provider package, and referencing that from
            // the Application layer would couple business logic to one database
            // vendor. The compiler enforced that boundary here. When Phase 8
            // needs genuinely provider-specific search (trigram indexes), the
            // answer is an interface declared in Application and implemented in
            // Infrastructure, not a package reference in the wrong direction.
            var lowered = term.ToLowerInvariant();

            query = query.Where(d =>
                d.Name.ToLower().Contains(lowered) ||
                d.Description.ToLower().Contains(lowered));
        }

        // Project BEFORE paging so the database returns only these columns, and
        // so the sort can reference EmployeeCount without a second pass.
        var projected = query.Select(d => new DepartmentDto
        {
            Id = d.Id,
            Name = d.Name,
            Description = d.Description,
            // Correlated subquery, evaluated server-side. One statement total.
            EmployeeCount = d.Employees.Count(),
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt
        });

        var sorted = projected.ApplySorting(
            parameters.SortBy,
            parameters.IsDescending,
            SortMap,
            defaultSort: d => d.Name,
            tiebreaker: d => d.Id);

        return await sorted.ToPagedResultAsync(parameters.Page, parameters.PageSize, cancellationToken);
    }

    public async Task<DepartmentDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var department = await _context.Departments
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                EmployeeCount = d.Employees.Count(),
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return department ?? throw new NotFoundException(nameof(Department), id);
    }

    public async Task<DepartmentDto> CreateAsync(
        CreateDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        // Checked here to return a clean 409 rather than letting the unique
        // index throw a DbUpdateException. The index is still the real
        // guarantee: this check alone would lose a race between two concurrent
        // requests.
        var exists = await _context.Departments
            .AsNoTracking()
            .AnyAsync(d => d.Name.ToLower() == name.ToLower(), cancellationToken);

        if (exists)
        {
            throw new ConflictException($"A department named '{name}' already exists.");
        }

        var department = new Department
        {
            Name = name,
            Description = request.Description.Trim()
            // CreatedAt is set by the SaveChanges override, not here.
        };

        _context.Departments.Add(department);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created department {DepartmentId} '{DepartmentName}'", department.Id, department.Name);

        return new DepartmentDto
        {
            Id = department.Id,
            Name = department.Name,
            Description = department.Description,
            EmployeeCount = 0,
            CreatedAt = department.CreatedAt,
            UpdatedAt = department.UpdatedAt
        };
    }

    public async Task<DepartmentDto> UpdateAsync(
        int id,
        UpdateDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        // Tracked, because this entity is about to be modified. The change
        // tracker produces an UPDATE containing only the columns that actually
        // changed.
        var department = await _context.Departments
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), id);

        var name = request.Name.Trim();

        // Excludes this row, so saving a department without renaming it is not
        // reported as a conflict with itself.
        var nameTaken = await _context.Departments
            .AsNoTracking()
            .AnyAsync(d => d.Id != id && d.Name.ToLower() == name.ToLower(), cancellationToken);

        if (nameTaken)
        {
            throw new ConflictException($"A department named '{name}' already exists.");
        }

        department.Name = name;
        department.Description = request.Description.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated department {DepartmentId}", id);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var department = await _context.Departments
            .Include(d => d.Employees)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), id);

        // The FK is Restrict, so the database would reject this anyway — but as
        // an opaque DbUpdateException surfacing as a 500. Checking here returns
        // a 422 that explains what to do about it.
        if (department.Employees.Count > 0)
        {
            throw new BusinessRuleViolationException(
                $"Department '{department.Name}' still has {department.Employees.Count} employee(s). " +
                "Reassign them before deleting it.");
        }

        _context.Departments.Remove(department);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted department {DepartmentId}", id);
    }
}
