using System.Net.Mime;
using EnterpriseManagement.Api.Authorization;
using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Employees.Dtos;
using EnterpriseManagement.Application.Features.Employees.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseManagement.Api.Controllers;

/// <summary>Employee records.</summary>
/// <remarks>
/// Reading is open to any authenticated user — an employee directory is
/// ordinary internal information, and tasks and projects are meaningless
/// without it. Writing is restricted to ADMIN and MANAGER.
/// </remarks>
[ApiController]
[Route("api/employees")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeesController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    /// <summary>Lists employees with paging, search, filtering and sorting.</summary>
    /// <remarks>
    /// Examples:
    /// <list type="bullet">
    /// <item><c>GET /api/employees?page=1&amp;pageSize=20</c></item>
    /// <item><c>GET /api/employees?departmentId=2&amp;isActive=true</c></item>
    /// <item><c>GET /api/employees?search=john</c> — matches first name, last name, email or employee code</item>
    /// <item><c>GET /api/employees?sortBy=hireDate&amp;sortOrder=desc</c></item>
    /// </list>
    /// <para>Sortable: employeeCode, firstName, lastName, email, jobTitle, hireDate, createdAt.</para>
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EmployeeListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<EmployeeListDto>>> GetAll(
        [FromQuery] EmployeeQueryParameters parameters,
        CancellationToken cancellationToken) =>
        Ok(await _employeeService.GetAsync(parameters, cancellationToken));

    /// <summary>Gets a single employee.</summary>
    [HttpGet("{id:int}", Name = nameof(GetEmployeeById))]
    [ProducesResponseType(typeof(EmployeeListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeListDto>> GetEmployeeById(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await _employeeService.GetByIdAsync(id, cancellationToken));

    /// <summary>Creates an employee. ADMIN or MANAGER.</summary>
    /// <response code="409">Employee code or email already in use, or the user is already linked.</response>
    /// <response code="422">Referenced department or user does not exist.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [ProducesResponseType(typeof(EmployeeListDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EmployeeListDto>> Create(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var employee = await _employeeService.CreateAsync(request, cancellationToken);

        return CreatedAtRoute(nameof(GetEmployeeById), new { id = employee.Id }, employee);
    }

    /// <summary>Updates an employee. The employee code cannot be changed.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [ProducesResponseType(typeof(EmployeeListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EmployeeListDto>> Update(
        int id,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _employeeService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Deletes an employee with no project history. ADMIN only.</summary>
    /// <remarks>
    /// Restricted more tightly than create and update: deleting an employee is
    /// irreversible and destroys an HR record. Anyone with project history must
    /// be deactivated instead, so past work stays attributable.
    /// </remarks>
    /// <response code="422">The employee manages projects or has assignment history.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _employeeService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
