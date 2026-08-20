using System.Net.Mime;
using EnterpriseManagement.Api.Authorization;
using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Projects.Dtos;
using EnterpriseManagement.Application.Features.Projects.Services;
using EnterpriseManagement.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseManagement.Api.Controllers;

/// <summary>Projects and their team membership.</summary>
/// <remarks>
/// <para>
/// Access here is not expressible with role attributes alone. A MANAGER may
/// edit the projects they manage and no others, which depends on the specific
/// project. Writes therefore load the project first and ask
/// <see cref="IAuthorizationService"/>, which runs
/// <c>ProjectAuthorizationHandler</c>.
/// </para>
/// <para>
/// Reads use a different mechanism: the service applies a row-level scope so a
/// list contains only projects the caller may see. A per-resource handler cannot
/// do that job without materialising every row, which would break paging.
/// </para>
/// </remarks>
[ApiController]
[Route("api/projects")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IAuthorizationService _authorizationService;

    public ProjectsController(
        IProjectService projectService,
        IAuthorizationService authorizationService)
    {
        _projectService = projectService;
        _authorizationService = authorizationService;
    }

    /// <summary>Lists projects visible to the caller.</summary>
    /// <remarks>
    /// ADMIN sees every project. Everyone else sees only projects they manage or
    /// are currently assigned to. The scope is applied before paging, so the
    /// total count cannot leak the existence of hidden projects.
    /// <para>Examples: <c>?status=1</c>, <c>?managerEmployeeId=5</c>, <c>?search=apollo</c>,
    /// <c>?sortBy=startDate&amp;sortOrder=desc</c></para>
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProjectListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProjectListDto>>> GetAll(
        [FromQuery] ProjectQueryParameters parameters,
        CancellationToken cancellationToken) =>
        Ok(await _projectService.GetAsync(parameters, cancellationToken));

    /// <summary>Gets one project, if the caller may see it.</summary>
    /// <response code="404">No such project, or it is outside the caller's scope.</response>
    [HttpGet("{id:int}", Name = nameof(GetProjectById))]
    [ProducesResponseType(typeof(ProjectListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectListDto>> GetProjectById(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await _projectService.GetByIdAsync(id, cancellationToken));

    /// <summary>Creates a project. ADMIN or MANAGER.</summary>
    /// <remarks>
    /// A plain role check suffices here: there is no existing resource to own,
    /// so there is nothing for a resource handler to compare against.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [ProducesResponseType(typeof(ProjectListDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProjectListDto>> Create(
        [FromBody] CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _projectService.CreateAsync(request, cancellationToken);

        return CreatedAtRoute(nameof(GetProjectById), new { id = project.Id }, project);
    }

    /// <summary>Updates a project. ADMIN, or the MANAGER who owns it.</summary>
    /// <response code="403">Authenticated, but not this project's manager.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProjectListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProjectListDto>> Update(
        int id,
        [FromBody] UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        await AuthorizeProjectAsync(id, ProjectOperations.Manage, cancellationToken);

        return Ok(await _projectService.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>Deletes a project that has no tasks. ADMIN only.</summary>
    /// <response code="422">The project still has tasks.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        // ChangeOwnership rather than Manage: deletion is destructive and
        // administrative, so a project's own manager is not sufficient.
        await AuthorizeProjectAsync(id, ProjectOperations.ChangeOwnership, cancellationToken);

        await _projectService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>Lists the project's team.</summary>
    /// <param name="id">Project id.</param>
    /// <param name="includeFormer">Include members who have left. Defaults to false.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("{id:int}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProjectMemberDto>>> GetMembers(
        int id,
        [FromQuery] bool includeFormer,
        CancellationToken cancellationToken) =>
        Ok(await _projectService.GetMembersAsync(id, includeFormer, cancellationToken));

    /// <summary>Assigns an employee to the project. ADMIN, or the owning MANAGER.</summary>
    /// <response code="409">The employee is already a current member.</response>
    /// <response code="422">The employee does not exist or is inactive.</response>
    [HttpPost("{id:int}/members")]
    [ProducesResponseType(typeof(ProjectMemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProjectMemberDto>> AssignEmployee(
        int id,
        [FromBody] AssignEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        await AuthorizeProjectAsync(id, ProjectOperations.Manage, cancellationToken);

        var member = await _projectService.AssignEmployeeAsync(id, request, cancellationToken);

        return CreatedAtAction(nameof(GetMembers), new { id }, member);
    }

    /// <summary>Removes an employee from the project. ADMIN, or the owning MANAGER.</summary>
    /// <remarks>
    /// The membership row is stamped with an end date rather than deleted, so
    /// the record of who worked on the project survives.
    /// </remarks>
    [HttpDelete("{id:int}/members/{employeeId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnassignEmployee(
        int id,
        int employeeId,
        CancellationToken cancellationToken)
    {
        await AuthorizeProjectAsync(id, ProjectOperations.Manage, cancellationToken);

        await _projectService.UnassignEmployeeAsync(id, employeeId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Loads the project's ownership details and evaluates the requirement
    /// against them.
    /// </summary>
    /// <remarks>
    /// Throws rather than returning a result, so every caller either passes or
    /// stops. A helper that returned a bool would rely on each action
    /// remembering to check it.
    /// <para>
    /// <c>Forbid()</c> produces a 403 with no body, which is what ASP.NET Core
    /// returns for a failed policy anywhere else — consistency matters more
    /// here than a custom message, and a detailed reason would tell a caller
    /// about resources they cannot access.
    /// </para>
    /// </remarks>
    private async Task AuthorizeProjectAsync(
        int projectId,
        OperationAuthorizationRequirement operation,
        CancellationToken cancellationToken)
    {
        // Throws NotFoundException for a missing project, so a nonexistent id
        // reads as 404 rather than a misleading 403.
        var accessInfo = await _projectService.GetAccessInfoAsync(projectId, cancellationToken);

        var result = await _authorizationService.AuthorizeAsync(User, accessInfo, operation);

        if (!result.Succeeded)
        {
            throw new ForbiddenException(
                $"You are not permitted to {operation.Name.ToLowerInvariant()} this project.");
        }
    }
}
