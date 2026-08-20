using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Tasks.Dtos;

namespace EnterpriseManagement.Application.Features.Tasks.Services;

public interface ITaskService
{
    Task<PagedResult<TaskListDto>> GetAsync(
        TaskQueryParameters parameters, CancellationToken cancellationToken = default);

    Task<TaskListDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Loads the fields an authorisation decision about this task needs.</summary>
    Task<TaskAccessInfo> GetAccessInfoAsync(int id, CancellationToken cancellationToken = default);

    Task<TaskListDto> CreateAsync(
        CreateTaskRequest request, CancellationToken cancellationToken = default);

    Task<TaskListDto> UpdateAsync(
        int id, UpdateTaskRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a task to a new status, rejecting transitions the workflow forbids.
    /// </summary>
    Task<TaskListDto> UpdateStatusAsync(
        int id, UpdateTaskStatusRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
