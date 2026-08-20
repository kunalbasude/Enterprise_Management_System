using System.Linq.Expressions;
using EnterpriseManagement.Application.Common.Extensions;
using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Projects.Dtos;
using EnterpriseManagement.Domain.Common;
using EnterpriseManagement.Domain.Entities;
using EnterpriseManagement.Domain.Enums;
using EnterpriseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnterpriseManagement.Application.Features.Projects.Services;

public class ProjectService : IProjectService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProjectService> _logger;

    private static readonly Dictionary<string, Expression<Func<ProjectListDto, object>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = p => p.Name,
            ["code"] = p => p.Code,
            ["status"] = p => p.Status,
            ["startDate"] = p => p.StartDate,
            ["endDate"] = p => p.EndDate ?? DateOnly.MaxValue,
            ["createdAt"] = p => p.CreatedAt
        };

    public ProjectService(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        ILogger<ProjectService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PagedResult<ProjectListDto>> GetAsync(
        ProjectQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyVisibilityScope(_context.Projects.AsNoTracking());

        if (parameters.Status.HasValue)
        {
            query = query.Where(p => p.Status == parameters.Status.Value);
        }

        if (parameters.ManagerEmployeeId.HasValue)
        {
            query = query.Where(p => p.ManagerEmployeeId == parameters.ManagerEmployeeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var term = parameters.Search.Trim().ToLowerInvariant();

            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Code.ToLower().Contains(term));
        }

        var projected = query.Select(p => new ProjectListDto
        {
            Id = p.Id,
            Name = p.Name,
            Code = p.Code,
            Description = p.Description,
            Status = p.Status,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            ManagerEmployeeId = p.ManagerEmployeeId,
            ManagerName = p.Manager.FirstName + " " + p.Manager.LastName,
            // Correlated subqueries, evaluated by the database in the same
            // statement. Loading the collections to count them in memory would
            // be one extra query per project — the classic N+1.
            TeamSize = p.TeamMembers.Count(tm => tm.UnassignedAt == null),
            TaskCount = p.Tasks.Count(),
            CompletedTaskCount = p.Tasks.Count(t => t.Status == TaskItemStatus.Done),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        });

        var sorted = projected.ApplySorting(
            parameters.SortBy,
            parameters.IsDescending,
            SortMap,
            defaultSort: p => p.StartDate,
            tiebreaker: p => p.Id);

        return await sorted.ToPagedResultAsync(parameters.Page, parameters.PageSize, cancellationToken);
    }

    /// <summary>
    /// Restricts a project query to what the caller is allowed to see.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Row-level filtering rather than resource-based authorisation, because the
    /// two solve different problems. A handler decides about ONE resource that
    /// has already been loaded; it cannot be applied to a paged list without
    /// fetching every row and testing each — which breaks paging, since you
    /// cannot know how many rows survive without materialising all of them.
    /// </para>
    /// <para>
    /// Applied before paging, so the COUNT and the page both reflect the scope.
    /// A filter applied after paging would leak the existence of hidden rows
    /// through the total count.
    /// </para>
    /// </remarks>
    private IQueryable<Project> ApplyVisibilityScope(IQueryable<Project> query)
    {
        if (_currentUser.IsInRole(RoleNames.Admin))
        {
            return query;
        }

        var employeeId = _currentUser.EmployeeId;

        if (employeeId is null)
        {
            // An account with no employee record is on no projects and manages
            // none, so it sees nothing rather than everything. Failing closed.
            return query.Where(_ => false);
        }

        return query.Where(p =>
            p.ManagerEmployeeId == employeeId.Value ||
            p.TeamMembers.Any(tm => tm.EmployeeId == employeeId.Value && tm.UnassignedAt == null));
    }

    public async Task<ProjectListDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var project = await ApplyVisibilityScope(_context.Projects.AsNoTracking())
            .Where(p => p.Id == id)
            .Select(p => new ProjectListDto
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                Description = p.Description,
                Status = p.Status,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                ManagerEmployeeId = p.ManagerEmployeeId,
                ManagerName = p.Manager.FirstName + " " + p.Manager.LastName,
                TeamSize = p.TeamMembers.Count(tm => tm.UnassignedAt == null),
                TaskCount = p.Tasks.Count(),
                CompletedTaskCount = p.Tasks.Count(t => t.Status == TaskItemStatus.Done),
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Out-of-scope projects are reported as not found rather than forbidden,
        // so a caller cannot probe for the existence of projects they cannot see.
        return project ?? throw new NotFoundException(nameof(Project), id);
    }

    public async Task<ProjectAccessInfo> GetAccessInfoAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        // Not scoped: the authorisation handler makes the access decision, and
        // scoping here would turn every legitimate 403 into a confusing 404.
        var info = await _context.Projects
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProjectAccessInfo(p.Id, p.ManagerEmployeeId))
            .FirstOrDefaultAsync(cancellationToken);

        return info ?? throw new NotFoundException(nameof(Project), id);
    }

    public async Task<ProjectListDto> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();

        if (await _context.Projects.AsNoTracking().AnyAsync(p => p.Code == code, cancellationToken))
        {
            throw new ConflictException($"Project code '{code}' is already in use.");
        }

        await EnsureManagerExistsAsync(request.ManagerEmployeeId, cancellationToken);
        EnsureDatesAreOrdered(request.StartDate, request.EndDate);

        var project = new Project
        {
            Name = request.Name.Trim(),
            Code = code,
            Description = request.Description.Trim(),
            Status = request.Status,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ManagerEmployeeId = request.ManagerEmployeeId
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created project {ProjectId} ({ProjectCode}) managed by employee {ManagerEmployeeId}",
            project.Id, code, request.ManagerEmployeeId);

        return await GetByIdAsync(project.Id, cancellationToken);
    }

    public async Task<ProjectListDto> UpdateAsync(
        int id,
        UpdateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Project), id);

        EnsureDatesAreOrdered(request.StartDate, request.EndDate);

        // Reassignment is an ownership change. The controller authorises it
        // separately, but the rule is repeated here because the service is the
        // last line of defence: a future endpoint that forgets the check must
        // not become a privilege escalation route.
        if (request.ManagerEmployeeId != project.ManagerEmployeeId)
        {
            if (!_currentUser.IsInRole(RoleNames.Admin))
            {
                throw new BusinessRuleViolationException(
                    "Only an administrator may change a project's manager.");
            }

            await EnsureManagerExistsAsync(request.ManagerEmployeeId, cancellationToken);

            _logger.LogWarning(
                "Project {ProjectId} reassigned from employee {OldManager} to {NewManager} by user {ActorId}",
                id, project.ManagerEmployeeId, request.ManagerEmployeeId, _currentUser.UserId);

            project.ManagerEmployeeId = request.ManagerEmployeeId;
        }

        project.Name = request.Name.Trim();
        project.Description = request.Description.Trim();
        project.Status = request.Status;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated project {ProjectId}", id);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Project), id);

        var taskCount = await _context.Tasks
            .AsNoTracking()
            .CountAsync(t => t.ProjectId == id, cancellationToken);

        // tasks.project_id is Cascade, so the database would happily delete them
        // all. That is correct for referential integrity and wrong as a user
        // experience: destroying work silently is not something an API should
        // do on a single DELETE.
        if (taskCount > 0)
        {
            throw new BusinessRuleViolationException(
                $"This project has {taskCount} task(s). Cancel the project instead, " +
                "or delete its tasks first if the data is genuinely unwanted.");
        }

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Deleted project {ProjectId} by user {ActorId}", id, _currentUser.UserId);
    }

    public async Task<IReadOnlyList<ProjectMemberDto>> GetMembersAsync(
        int projectId,
        bool includeFormer,
        CancellationToken cancellationToken = default)
    {
        // Ensures the project exists and is visible to the caller before
        // returning its team.
        _ = await GetByIdAsync(projectId, cancellationToken);

        var query = _context.ProjectEmployees
            .AsNoTracking()
            .Where(pe => pe.ProjectId == projectId);

        if (!includeFormer)
        {
            query = query.Where(pe => pe.UnassignedAt == null);
        }

        return await query
            .OrderBy(pe => pe.Employee.LastName)
            .ThenBy(pe => pe.Employee.FirstName)
            .Select(pe => new ProjectMemberDto
            {
                AssignmentId = pe.Id,
                EmployeeId = pe.EmployeeId,
                EmployeeCode = pe.Employee.EmployeeCode,
                FullName = pe.Employee.FirstName + " " + pe.Employee.LastName,
                Email = pe.Employee.Email,
                JobTitle = pe.Employee.JobTitle,
                RoleOnProject = pe.RoleOnProject,
                AssignedAt = pe.AssignedAt,
                UnassignedAt = pe.UnassignedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectMemberDto> AssignEmployeeAsync(
        int projectId,
        AssignEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        var projectExists = await _context.Projects
            .AsNoTracking()
            .AnyAsync(p => p.Id == projectId, cancellationToken);

        if (!projectExists)
        {
            throw new NotFoundException(nameof(Project), projectId);
        }

        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new BusinessRuleViolationException($"Employee {request.EmployeeId} does not exist.");

        if (!employee.IsActive)
        {
            throw new BusinessRuleViolationException(
                $"Employee {employee.EmployeeCode} is not active and cannot be assigned to a project.");
        }

        // Matches the filtered unique index exactly: current membership only.
        // A previous, ended assignment does not block rejoining.
        var alreadyCurrent = await _context.ProjectEmployees
            .AsNoTracking()
            .AnyAsync(
                pe => pe.ProjectId == projectId
                      && pe.EmployeeId == request.EmployeeId
                      && pe.UnassignedAt == null,
                cancellationToken);

        if (alreadyCurrent)
        {
            throw new ConflictException(
                $"Employee {employee.EmployeeCode} is already assigned to this project.");
        }

        var assignment = new ProjectEmployee
        {
            ProjectId = projectId,
            EmployeeId = request.EmployeeId,
            RoleOnProject = request.RoleOnProject.Trim(),
            AssignedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        _context.ProjectEmployees.Add(assignment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Assigned employee {EmployeeId} to project {ProjectId} as {RoleOnProject}",
            request.EmployeeId, projectId, assignment.RoleOnProject);

        return new ProjectMemberDto
        {
            AssignmentId = assignment.Id,
            EmployeeId = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            FullName = $"{employee.FirstName} {employee.LastName}",
            Email = employee.Email,
            JobTitle = employee.JobTitle,
            RoleOnProject = assignment.RoleOnProject,
            AssignedAt = assignment.AssignedAt,
            UnassignedAt = null
        };
    }

    public async Task UnassignEmployeeAsync(
        int projectId,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _context.ProjectEmployees
            .FirstOrDefaultAsync(
                pe => pe.ProjectId == projectId
                      && pe.EmployeeId == employeeId
                      && pe.UnassignedAt == null,
                cancellationToken)
            ?? throw new NotFoundException(
                $"Employee {employeeId} is not currently assigned to project {projectId}.");

        // Stamped, not deleted. The row is the record that this person worked on
        // this project, which tasks and reports still refer to.
        assignment.UnassignedAt = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Unassigned employee {EmployeeId} from project {ProjectId}", employeeId, projectId);
    }

    private async Task EnsureManagerExistsAsync(int managerEmployeeId, CancellationToken cancellationToken)
    {
        var manager = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == managerEmployeeId, cancellationToken)
            ?? throw new BusinessRuleViolationException($"Employee {managerEmployeeId} does not exist.");

        if (!manager.IsActive)
        {
            throw new BusinessRuleViolationException(
                $"Employee {manager.EmployeeCode} is not active and cannot manage a project.");
        }
    }

    private static void EnsureDatesAreOrdered(DateOnly startDate, DateOnly? endDate)
    {
        if (endDate.HasValue && endDate.Value < startDate)
        {
            throw new BusinessRuleViolationException("The end date must not be before the start date.");
        }
    }
}
