using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Departments.Dtos;

namespace EnterpriseManagement.Application.Features.Departments.Services;

public interface IDepartmentService
{
    Task<PagedResult<DepartmentDto>> GetAsync(
        DepartmentQueryParameters parameters, CancellationToken cancellationToken = default);

    Task<DepartmentDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<DepartmentDto> CreateAsync(
        CreateDepartmentRequest request, CancellationToken cancellationToken = default);

    Task<DepartmentDto> UpdateAsync(
        int id, UpdateDepartmentRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
