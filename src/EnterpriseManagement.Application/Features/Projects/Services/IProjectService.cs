using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Projects.Dtos;

namespace EnterpriseManagement.Application.Features.Projects.Services;

public interface IProjectService
{
    Task<PagedResult<ProjectListDto>> GetAsync(
        ProjectQueryParameters parameters, CancellationToken cancellationToken = default);

    Task<ProjectListDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the two fields an authorisation decision needs.
    /// </summary>
    /// <remarks>
    /// Called by controllers before a write, so the resource exists to authorise
    /// against. Throws <c>NotFoundException</c> for a missing project, which
    /// means a caller learns "no such project" rather than "forbidden" — correct
    /// here, since project ids are not sensitive and a 403 for a nonexistent id
    /// would be misleading.
    /// </remarks>
    Task<ProjectAccessInfo> GetAccessInfoAsync(int id, CancellationToken cancellationToken = default);

    Task<ProjectListDto> CreateAsync(
        CreateProjectRequest request, CancellationToken cancellationToken = default);

    Task<ProjectListDto> UpdateAsync(
        int id, UpdateProjectRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectMemberDto>> GetMembersAsync(
        int projectId, bool includeFormer, CancellationToken cancellationToken = default);

    Task<ProjectMemberDto> AssignEmployeeAsync(
        int projectId, AssignEmployeeRequest request, CancellationToken cancellationToken = default);

    Task UnassignEmployeeAsync(
        int projectId, int employeeId, CancellationToken cancellationToken = default);
}
