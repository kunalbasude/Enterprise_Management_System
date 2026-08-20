using System.Linq.Expressions;
using EnterpriseManagement.Application.Common.Extensions;
using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Employees.Dtos;
using EnterpriseManagement.Domain.Entities;
using EnterpriseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnterpriseManagement.Application.Features.Employees.Services;

public class EmployeeService : IEmployeeService
{
    private readonly IApplicationDbContext _context;
    private readonly IEmployeeSearch _search;
    private readonly ILogger<EmployeeService> _logger;

    private static readonly Dictionary<string, Expression<Func<EmployeeListDto, object>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["employeeCode"] = e => e.EmployeeCode,
            ["firstName"] = e => e.FirstName,
            ["lastName"] = e => e.LastName,
            ["email"] = e => e.Email,
            ["jobTitle"] = e => e.JobTitle,
            ["hireDate"] = e => e.HireDate,
            ["createdAt"] = e => e.CreatedAt
        };

    public EmployeeService(
        IApplicationDbContext context,
        IEmployeeSearch search,
        ILogger<EmployeeService> logger)
    {
        _context = context;
        _search = search;
        _logger = logger;
    }

    public async Task<PagedResult<EmployeeListDto>> GetAsync(
        EmployeeQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Employees.AsNoTracking().AsQueryable();

        // Filters applied before search so the search runs over the smallest
        // possible set. The planner may reorder these, but expressing the
        // cheap, index-backed predicates first costs nothing.
        if (parameters.DepartmentId.HasValue)
        {
            query = query.Where(e => e.DepartmentId == parameters.DepartmentId.Value);
        }

        if (parameters.IsActive.HasValue)
        {
            query = query.Where(e => e.IsActive == parameters.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            // Delegated to the provider-specific implementation. This layer does
            // not know whether that ends up as ILIKE, a trigram match or a
            // full-text query, and does not need to.
            query = _search.ApplySearch(query, parameters.Search);
        }

        var projected = query.Select(e => new EmployeeListDto
        {
            Id = e.Id,
            EmployeeCode = e.EmployeeCode,
            FirstName = e.FirstName,
            LastName = e.LastName,
            // Concatenated by the database. The entity's FullName property is
            // C#-only and cannot be translated, which is why it is Ignore()d in
            // the EF configuration.
            FullName = e.FirstName + " " + e.LastName,
            Email = e.Email,
            PhoneNumber = e.PhoneNumber,
            JobTitle = e.JobTitle,
            HireDate = e.HireDate,
            IsActive = e.IsActive,
            DepartmentId = e.DepartmentId,
            // A join the database performs, not an Include that would load the
            // whole Department entity for one string.
            DepartmentName = e.Department.Name,
            UserId = e.UserId,
            ActiveProjectCount = e.ProjectAssignments.Count(pa => pa.UnassignedAt == null),
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        });

        var sorted = projected.ApplySorting(
            parameters.SortBy,
            parameters.IsDescending,
            SortMap,
            // Matches ix_employees_last_name_first_name, so the default listing
            // can walk the index in order instead of sorting the table.
            defaultSort: e => e.LastName,
            tiebreaker: e => e.Id);

        return await sorted.ToPagedResultAsync(parameters.Page, parameters.PageSize, cancellationToken);
    }

    public async Task<EmployeeListDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var employee = await ProjectSingle(id).FirstOrDefaultAsync(cancellationToken);

        return employee ?? throw new NotFoundException(nameof(Employee), id);
    }

    public async Task<EmployeeListDto> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = request.EmployeeCode.Trim().ToUpperInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        await EnsureDepartmentExistsAsync(request.DepartmentId, cancellationToken);
        await EnsureCodeIsFreeAsync(code, excludingId: null, cancellationToken);
        await EnsureEmailIsFreeAsync(email, excludingId: null, cancellationToken);
        await EnsureUserIsLinkableAsync(request.UserId, excludingEmployeeId: null, cancellationToken);

        var employee = new Employee
        {
            EmployeeCode = code,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            JobTitle = request.JobTitle.Trim(),
            HireDate = request.HireDate,
            DepartmentId = request.DepartmentId,
            UserId = request.UserId,
            IsActive = true
        };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created employee {EmployeeId} ({EmployeeCode})", employee.Id, code);

        return await GetByIdAsync(employee.Id, cancellationToken);
    }

    public async Task<EmployeeListDto> UpdateAsync(
        int id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), id);

        var email = request.Email.Trim().ToLowerInvariant();

        await EnsureDepartmentExistsAsync(request.DepartmentId, cancellationToken);
        await EnsureEmailIsFreeAsync(email, excludingId: id, cancellationToken);
        await EnsureUserIsLinkableAsync(request.UserId, excludingEmployeeId: id, cancellationToken);

        employee.FirstName = request.FirstName.Trim();
        employee.LastName = request.LastName.Trim();
        employee.Email = email;
        employee.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        employee.JobTitle = request.JobTitle.Trim();
        employee.HireDate = request.HireDate;
        employee.DepartmentId = request.DepartmentId;
        employee.IsActive = request.IsActive;
        employee.UserId = request.UserId;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated employee {EmployeeId}", id);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), id);

        // project_employees.employee_id is Restrict, and so is
        // projects.manager_employee_id. Both would surface as an opaque
        // DbUpdateException, so they are checked here to explain the remedy.
        var managedProjects = await _context.Projects
            .AsNoTracking()
            .CountAsync(p => p.ManagerEmployeeId == id, cancellationToken);

        if (managedProjects > 0)
        {
            throw new BusinessRuleViolationException(
                $"This employee manages {managedProjects} project(s). Reassign them before deleting.");
        }

        var assignments = await _context.ProjectEmployees
            .AsNoTracking()
            .CountAsync(pe => pe.EmployeeId == id, cancellationToken);

        if (assignments > 0)
        {
            throw new BusinessRuleViolationException(
                $"This employee has {assignments} project assignment(s) on record. " +
                "Deactivate them instead, so project history stays intact.");
        }

        _context.Employees.Remove(employee);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted employee {EmployeeId}", id);
    }

    private IQueryable<EmployeeListDto> ProjectSingle(int id) =>
        _context.Employees
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new EmployeeListDto
            {
                Id = e.Id,
                EmployeeCode = e.EmployeeCode,
                FirstName = e.FirstName,
                LastName = e.LastName,
                FullName = e.FirstName + " " + e.LastName,
                Email = e.Email,
                PhoneNumber = e.PhoneNumber,
                JobTitle = e.JobTitle,
                HireDate = e.HireDate,
                IsActive = e.IsActive,
                DepartmentId = e.DepartmentId,
                DepartmentName = e.Department.Name,
                UserId = e.UserId,
                ActiveProjectCount = e.ProjectAssignments.Count(pa => pa.UnassignedAt == null),
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            });

    private async Task EnsureDepartmentExistsAsync(int departmentId, CancellationToken cancellationToken)
    {
        var exists = await _context.Departments
            .AsNoTracking()
            .AnyAsync(d => d.Id == departmentId, cancellationToken);

        if (!exists)
        {
            // 422, not 404: the employee route exists and the request is
            // well-formed. It is the referenced department that is wrong.
            throw new BusinessRuleViolationException($"Department {departmentId} does not exist.");
        }
    }

    private async Task EnsureCodeIsFreeAsync(string code, int? excludingId, CancellationToken cancellationToken)
    {
        var taken = await _context.Employees
            .AsNoTracking()
            .AnyAsync(e => e.EmployeeCode == code && (excludingId == null || e.Id != excludingId), cancellationToken);

        if (taken)
        {
            throw new ConflictException($"Employee code '{code}' is already in use.");
        }
    }

    private async Task EnsureEmailIsFreeAsync(string email, int? excludingId, CancellationToken cancellationToken)
    {
        var taken = await _context.Employees
            .AsNoTracking()
            .AnyAsync(e => e.Email == email && (excludingId == null || e.Id != excludingId), cancellationToken);

        if (taken)
        {
            throw new ConflictException($"Email '{email}' is already used by another employee.");
        }
    }

    private async Task EnsureUserIsLinkableAsync(
        int? userId,
        int? excludingEmployeeId,
        CancellationToken cancellationToken)
    {
        if (userId is null)
        {
            return;
        }

        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId, cancellationToken);

        if (!userExists)
        {
            throw new BusinessRuleViolationException($"User {userId} does not exist.");
        }

        // Enforced by the filtered unique index too; checked here for a clear
        // 409 rather than a database error.
        var alreadyLinked = await _context.Employees
            .AsNoTracking()
            .AnyAsync(
                e => e.UserId == userId && (excludingEmployeeId == null || e.Id != excludingEmployeeId),
                cancellationToken);

        if (alreadyLinked)
        {
            throw new ConflictException($"User {userId} is already linked to another employee.");
        }
    }
}
