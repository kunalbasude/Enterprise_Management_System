namespace EnterpriseManagement.Application.Features.Projects.Dtos;

/// <summary>
/// The minimum a project authorisation decision needs.
/// </summary>
/// <remarks>
/// Deliberately tiny. Resource-based authorisation must load the resource before
/// it can decide, so loading a full project entity to compare one integer would
/// mean fetching the description, dates and navigation properties on every
/// authorised write. This projects two columns.
/// </remarks>
/// <param name="ProjectId">The project being acted on.</param>
/// <param name="ManagerEmployeeId">The employee accountable for it.</param>
public record ProjectAccessInfo(int ProjectId, int ManagerEmployeeId);
