using System.Linq.Expressions;
using EnterpriseManagement.Application.Common.Extensions;
using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Tasks.Dtos;
using EnterpriseManagement.Domain.Common;
using EnterpriseManagement.Domain.Entities;
using EnterpriseManagement.Domain.Enums;
using EnterpriseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnterpriseManagement.Application.Features.Tasks.Services;

public class TaskService : ITaskService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TaskService> _logger;

    private static readonly Dictionary<string, Expression<Func<TaskListDto, object>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = t => t.Title,
            ["status"] = t => t.Status,
            ["priority"] = t => t.Priority,
            // Tasks with no due date sort last rather than in a provider-defined
            // position, so the ordering is total and paging is stable.
            ["dueDate"] = t => t.DueDate ?? DateOnly.MaxValue,
            ["createdAt"] = t => t.CreatedAt
        };

    public TaskService(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        ILogger<TaskService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PagedResult<TaskListDto>> GetAsync(
        TaskQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        var query = ApplyVisibilityScope(_context.Tasks.AsNoTracking());

        if (parameters.ProjectId.HasValue)
        {
            query = query.Where(t => t.ProjectId == parameters.ProjectId.Value);
        }

        if (parameters.Status.HasValue)
        {
            query = query.Where(t => t.Status == parameters.Status.Value);
        }

        if (parameters.Priority.HasValue)
        {
            query = query.Where(t => t.Priority == parameters.Priority.Value);
        }

        if (parameters.AssignedEmployeeId.HasValue)
        {
            query = query.Where(t => t.AssignedEmployeeId == parameters.AssignedEmployeeId.Value);
        }

        if (parameters.IsOverdue.HasValue)
        {
            query = parameters.IsOverdue.Value
                ? query.Where(t => t.DueDate != null
                                   && t.DueDate < today
                                   && t.Status != TaskItemStatus.Done
                                   && t.Status != TaskItemStatus.Cancelled)
                : query.Where(t => t.DueDate == null
                                   || t.DueDate >= today
                                   || t.Status == TaskItemStatus.Done
                                   || t.Status == TaskItemStatus.Cancelled);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var term = parameters.Search.Trim().ToLowerInvariant();

            query = query.Where(t => t.Title.ToLower().Contains(term));
        }

        var projected = query.Select(Projection(today));

        var sorted = projected.ApplySorting(
            parameters.SortBy,
            parameters.IsDescending,
            SortMap,
            defaultSort: t => t.DueDate ?? DateOnly.MaxValue,
            tiebreaker: t => t.Id);

        return await sorted.ToPagedResultAsync(parameters.Page, parameters.PageSize, cancellationToken);
    }

    /// <summary>
    /// The projection, shared by list and single-item reads so they cannot drift.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns an <see cref="Expression{TDelegate}"/>, not a <c>TaskListDto</c>.
    /// That distinction is not cosmetic. A plain method cannot be translated to
    /// SQL, so EF Core falls back to CLIENT evaluation of the final projection:
    /// it materialises whole entities and runs the method in memory, where the
    /// navigation properties are null because nothing loaded them. That produced
    /// a NullReferenceException on t.Project.Name during this phase, and would
    /// otherwise have been an N+1 with lazy loading enabled.
    /// </para>
    /// <para>
    /// An expression tree is data EF can inspect and turn into a SELECT with
    /// joins, so only the DTO's columns are ever fetched.
    /// </para>
    /// </remarks>
    private static Expression<Func<TaskItem, TaskListDto>> Projection(DateOnly today) => t => new TaskListDto
    {
        Id = t.Id,
        Title = t.Title,
        Description = t.Description,
        Status = t.Status,
        Priority = t.Priority,
        ProjectId = t.ProjectId,
        ProjectName = t.Project.Name,
        ProjectCode = t.Project.Code,
        AssignedEmployeeId = t.AssignedEmployeeId,
        AssignedEmployeeName = t.AssignedEmployee != null
            ? t.AssignedEmployee.FirstName + " " + t.AssignedEmployee.LastName
            : null,
        DueDate = t.DueDate,
        CompletedAt = t.CompletedAt,
        EstimatedHours = t.EstimatedHours,
        // Finished work is never overdue, however late it was completed.
        IsOverdue = t.DueDate != null
                    && t.DueDate < today
                    && t.Status != TaskItemStatus.Done
                    && t.Status != TaskItemStatus.Cancelled,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };

    /// <summary>
    /// Restricts a task query to what the caller may see.
    /// </summary>
    /// <remarks>
    /// A task is visible if the caller manages its project, is a current member
    /// of that project, or is assigned the task. The last clause matters on its
    /// own: someone can be given a task on a project they were never formally
    /// added to, and hiding their own work from them would be absurd.
    /// </remarks>
    private IQueryable<TaskItem> ApplyVisibilityScope(IQueryable<TaskItem> query)
    {
        if (_currentUser.IsInRole(RoleNames.Admin))
        {
            return query;
        }

        var employeeId = _currentUser.EmployeeId;

        if (employeeId is null)
        {
            return query.Where(_ => false);
        }

        return query.Where(t =>
            t.Project.ManagerEmployeeId == employeeId.Value ||
            t.AssignedEmployeeId == employeeId.Value ||
            t.Project.TeamMembers.Any(tm => tm.EmployeeId == employeeId.Value && tm.UnassignedAt == null));
    }

    public async Task<TaskListDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        var task = await ApplyVisibilityScope(_context.Tasks.AsNoTracking())
            .Where(t => t.Id == id)
            .Select(Projection(today))
            .FirstOrDefaultAsync(cancellationToken);

        // Out of scope reads as not found, so ids cannot be probed to discover
        // tasks on projects the caller has no part in.
        return task ?? throw new NotFoundException(nameof(TaskItem), id);
    }

    public async Task<TaskAccessInfo> GetAccessInfoAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        // Unscoped: the handler makes the access decision. Scoping here would
        // turn every legitimate 403 into a confusing 404.
        var info = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TaskAccessInfo(
                t.Id,
                t.ProjectId,
                t.Project.ManagerEmployeeId,
                t.AssignedEmployeeId))
            .FirstOrDefaultAsync(cancellationToken);

        return info ?? throw new NotFoundException(nameof(TaskItem), id);
    }

    public async Task<TaskListDto> CreateAsync(
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken)
            ?? throw new BusinessRuleViolationException($"Project {request.ProjectId} does not exist.");

        if (project.Status is ProjectStatus.Completed or ProjectStatus.Cancelled)
        {
            throw new BusinessRuleViolationException(
                $"Project {project.Code} is {project.Status} and cannot take new tasks.");
        }

        if (request.AssignedEmployeeId.HasValue)
        {
            await EnsureAssigneeIsValidAsync(
                request.ProjectId, request.AssignedEmployeeId.Value, cancellationToken);
        }

        var task = new TaskItem
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Status = TaskItemStatus.Todo,   // every task starts at the beginning
            Priority = request.Priority,
            ProjectId = request.ProjectId,
            AssignedEmployeeId = request.AssignedEmployeeId,
            DueDate = request.DueDate,
            EstimatedHours = request.EstimatedHours
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created task {TaskId} in project {ProjectId} assigned to {AssignedEmployeeId}",
            task.Id, request.ProjectId, request.AssignedEmployeeId);

        return await GetByIdAsync(task.Id, cancellationToken);
    }

    public async Task<TaskListDto> UpdateAsync(
        int id,
        UpdateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var task = await _context.Tasks
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(TaskItem), id);

        if (request.AssignedEmployeeId.HasValue &&
            request.AssignedEmployeeId != task.AssignedEmployeeId)
        {
            await EnsureAssigneeIsValidAsync(
                task.ProjectId, request.AssignedEmployeeId.Value, cancellationToken);
        }

        task.Title = request.Title.Trim();
        task.Description = request.Description.Trim();
        task.Priority = request.Priority;
        task.AssignedEmployeeId = request.AssignedEmployeeId;
        task.DueDate = request.DueDate;
        task.EstimatedHours = request.EstimatedHours;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated task {TaskId}", id);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<TaskListDto> UpdateStatusAsync(
        int id,
        UpdateTaskStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var task = await _context.Tasks
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(TaskItem), id);

        var from = task.Status;
        var to = request.Status;

        if (!TaskStatusTransitions.IsAllowed(from, to))
        {
            var allowed = TaskStatusTransitions.AllowedFrom(from);

            // The message names what IS permitted, so a client can recover
            // without reading the source or guessing.
            throw new BusinessRuleViolationException(
                allowed.Count == 0
                    ? $"A {from} task is final and cannot change status."
                    : $"A task cannot move from {from} to {to}. Allowed from {from}: {string.Join(", ", allowed)}.");
        }

        if (from == to)
        {
            // Idempotent: re-sending the current status is not an error, and
            // must not rewrite CompletedAt.
            return await GetByIdAsync(id, cancellationToken);
        }

        task.Status = to;

        // CompletedAt is maintained here rather than accepted from the client,
        // so a completion date can never contradict the status it belongs to.
        if (to == TaskItemStatus.Done)
        {
            task.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }
        else if (from == TaskItemStatus.Done)
        {
            // Reopened: clear the old completion time rather than leaving a
            // stale date on a task that is no longer finished.
            task.CompletedAt = null;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Status changes are the business event this system exists to track, so
        // they log the transition explicitly rather than just "updated".
        _logger.LogInformation(
            "Task {TaskId} moved from {FromStatus} to {ToStatus} by user {ActorId}",
            id, from, to, _currentUser.UserId);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var task = await _context.Tasks
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(TaskItem), id);

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Deleted task {TaskId} by user {ActorId}", id, _currentUser.UserId);
    }

    /// <summary>
    /// A task may only be assigned to an active employee who is on its project.
    /// </summary>
    /// <remarks>
    /// Without the membership check, work could be assigned to someone who
    /// cannot even see the project it belongs to — they would receive a task
    /// that does not appear in any of their lists.
    /// </remarks>
    private async Task EnsureAssigneeIsValidAsync(
        int projectId,
        int employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken)
            ?? throw new BusinessRuleViolationException($"Employee {employeeId} does not exist.");

        if (!employee.IsActive)
        {
            throw new BusinessRuleViolationException(
                $"Employee {employee.EmployeeCode} is not active and cannot be assigned tasks.");
        }

        var isOnProject = await _context.ProjectEmployees
            .AsNoTracking()
            .AnyAsync(
                pe => pe.ProjectId == projectId
                      && pe.EmployeeId == employeeId
                      && pe.UnassignedAt == null,
                cancellationToken);

        // The project manager is implicitly entitled to hold tasks on their own
        // project without being listed as a team member.
        var isProjectManager = await _context.Projects
            .AsNoTracking()
            .AnyAsync(p => p.Id == projectId && p.ManagerEmployeeId == employeeId, cancellationToken);

        if (!isOnProject && !isProjectManager)
        {
            throw new BusinessRuleViolationException(
                $"Employee {employee.EmployeeCode} is not assigned to this project. " +
                "Add them to the project team first.");
        }
    }
}
