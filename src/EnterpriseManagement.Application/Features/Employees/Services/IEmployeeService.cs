using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Employees.Dtos;

namespace EnterpriseManagement.Application.Features.Employees.Services;

public interface IEmployeeService
{
    Task<PagedResult<EmployeeListDto>> GetAsync(
        EmployeeQueryParameters parameters, CancellationToken cancellationToken = default);

    Task<EmployeeListDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<EmployeeListDto> CreateAsync(
        CreateEmployeeRequest request, CancellationToken cancellationToken = default);

    Task<EmployeeListDto> UpdateAsync(
        int id, UpdateEmployeeRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
