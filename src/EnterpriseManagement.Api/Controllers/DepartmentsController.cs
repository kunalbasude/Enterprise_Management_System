using System.Net.Mime;
using EnterpriseManagement.Api.Authorization;
using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Departments.Dtos;
using EnterpriseManagement.Application.Features.Departments.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseManagement.Api.Controllers;

/// <summary>Department management.</summary>
/// <remarks>
/// Reading is open to any authenticated user, because employees need to see the
/// department list to make sense of the rest of the system. Writing is
/// ADMIN-only: departments are organisational structure, not day-to-day data.
/// </remarks>
[ApiController]
[Route("api/departments")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    /// <summary>Lists departments with paging, search and sorting.</summary>
    /// <remarks>
    /// Example: <c>GET /api/departments?page=1&amp;pageSize=20&amp;search=eng&amp;sortBy=name&amp;sortOrder=asc</c>
    /// <para>Sortable fields: name, createdAt, employeeCount.</para>
    /// </remarks>
    /// <response code="200">A page of departments.</response>
    /// <response code="400">Invalid sort field or order.</response>
    /// <response code="401">No or invalid token.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DepartmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<DepartmentDto>>> GetAll(
        [FromQuery] DepartmentQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var result = await _departmentService.GetAsync(parameters, cancellationToken);

        return Ok(result);
    }

    /// <summary>Gets a single department.</summary>
    /// <response code="200">The department.</response>
    /// <response code="404">No department with that id.</response>
    [HttpGet("{id:int}", Name = nameof(GetDepartmentById))]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentDto>> GetDepartmentById(
        int id,
        CancellationToken cancellationToken)
    {
        // The :int route constraint means "/api/departments/abc" is a 404 from
        // routing rather than a model-binding 400, which keeps a nonsense URL
        // from looking like a validation failure.
        var department = await _departmentService.GetByIdAsync(id, cancellationToken);

        return Ok(department);
    }

    /// <summary>Creates a department. ADMIN only.</summary>
    /// <response code="201">Created. The Location header points at the new resource.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Caller is authenticated but not an ADMIN.</response>
    /// <response code="409">A department with that name already exists.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DepartmentDto>> Create(
        [FromBody] CreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var department = await _departmentService.CreateAsync(request, cancellationToken);

        return CreatedAtRoute(
            nameof(GetDepartmentById),
            new { id = department.Id },
            department);
    }

    /// <summary>Updates a department. ADMIN only.</summary>
    /// <response code="200">The updated department.</response>
    /// <response code="404">No department with that id.</response>
    /// <response code="409">Another department already uses that name.</response>
    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DepartmentDto>> Update(
        int id,
        [FromBody] UpdateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var department = await _departmentService.UpdateAsync(id, request, cancellationToken);

        return Ok(department);
    }

    /// <summary>Deletes an empty department. ADMIN only.</summary>
    /// <response code="204">Deleted. No body, because there is nothing left to return.</response>
    /// <response code="404">No department with that id.</response>
    /// <response code="422">The department still has employees.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _departmentService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
