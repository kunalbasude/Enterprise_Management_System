using System.Net.Mime;
using EnterpriseManagement.Api.Authorization;
using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Tasks.Dtos;
using EnterpriseManagement.Application.Features.Tasks.Services;
using EnterpriseManagement.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseManagement.Api.Controllers;

/// <summary>Tasks within projects.</summary>
/// <remarks>
/// Two levels of write access. Editing, reassigning and deleting belong to the
/// project's manager (or an ADMIN). Moving a task through its workflow is also
/// open to the person it is assigned to — an employee must be able to say "I
/// have started this" without being able to redefine the work.
/// </remarks>
[ApiController]
[Route("api/tasks")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly IAuthorizationService _authorizationService;

    public TasksController(ITaskService taskService, IAuthorizationService authorizationService)
    {
        _taskService = taskService;
        _authorizationService = authorizationService;
    }

    /// <summary>Lists tasks visible to the caller.</summary>
    /// <remarks>
    /// ADMIN sees all. Everyone else sees tasks on projects they manage or
    /// belong to, plus any task assigned to them personally.
    /// <para>Examples: <c>?projectId=3</c>, <c>?status=1</c>, <c>?assignedEmployeeId=5</c>,
    /// <c>?isOverdue=true</c>, <c>?sortBy=dueDate</c></para>
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TaskListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TaskListDto>>> GetAll(
        [FromQuery] TaskQueryParameters parameters,
        CancellationToken cancellationToken) =>
        Ok(await _taskService.GetAsync(parameters, cancellationToken));

    /// <summary>Gets one task, if the caller may see it.</summary>
    [HttpGet("{id:int}", Name = nameof(GetTaskById))]
    [ProducesResponseType(typeof(TaskListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskListDto>> GetTaskById(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await _taskService.GetByIdAsync(id, cancellationToken));

    /// <summary>Creates a task. ADMIN or MANAGER.</summary>
    /// <remarks>
    /// New tasks always start at Todo; the initial status is not caller-supplied,
    /// so nothing can be created already Done.
    /// </remarks>
    /// <response code="422">Project missing, closed, or the assignee is not on it.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [ProducesResponseType(typeof(TaskListDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskListDto>> Create(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await _taskService.CreateAsync(request, cancellationToken);

        return CreatedAtRoute(nameof(GetTaskById), new { id = task.Id }, task);
    }

    /// <summary>Updates a task's details. ADMIN, or the manager of its project.</summary>
    /// <remarks>
    /// Not open to the assignee: being given work does not confer the right to
    /// change its scope, deadline, or owner.
    /// </remarks>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(TaskListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskListDto>> Update(
        int id,
        [FromBody] UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        await AuthorizeTaskAsync(id, TaskOperations.Manage, cancellationToken);

        return Ok(await _taskService.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>
    /// Moves a task to a new status. ADMIN, the project's manager, or the assignee.
    /// </summary>
    /// <remarks>
    /// Its own endpoint rather than a field on the general update, because a
    /// status change is a workflow event with transition rules, and keeping it
    /// separate makes "who moved this to Done, and when" answerable.
    /// <para>
    /// Allowed moves: Todo to InProgress or Cancelled; InProgress to Todo,
    /// InReview, Done or Cancelled; InReview to InProgress, Done or Cancelled;
    /// Done to InProgress (reopen). Cancelled is final.
    /// </para>
    /// </remarks>
    /// <response code="422">The transition is not permitted from the task's current status.</response>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(TaskListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskListDto>> UpdateStatus(
        int id,
        [FromBody] UpdateTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        // PATCH rather than PUT: this modifies one field of the resource, it
        // does not replace it.
        await AuthorizeTaskAsync(id, TaskOperations.UpdateStatus, cancellationToken);

        return Ok(await _taskService.UpdateStatusAsync(id, request, cancellationToken));
    }

    /// <summary>Deletes a task. ADMIN, or the manager of its project.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await AuthorizeTaskAsync(id, TaskOperations.Manage, cancellationToken);

        await _taskService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }

    private async Task AuthorizeTaskAsync(
        int taskId,
        OperationAuthorizationRequirement operation,
        CancellationToken cancellationToken)
    {
        var accessInfo = await _taskService.GetAccessInfoAsync(taskId, cancellationToken);

        var result = await _authorizationService.AuthorizeAsync(User, accessInfo, operation);

        if (!result.Succeeded)
        {
            throw new ForbiddenException("You are not permitted to perform this action on this task.");
        }
    }
}
