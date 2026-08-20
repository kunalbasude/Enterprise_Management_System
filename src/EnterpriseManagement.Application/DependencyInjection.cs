using System.Reflection;
using EnterpriseManagement.Application.Features.Auth.Services;
using EnterpriseManagement.Application.Features.AuditLogs.Services;
using EnterpriseManagement.Application.Features.Dashboard.Services;
using EnterpriseManagement.Application.Features.Departments.Services;
using EnterpriseManagement.Application.Features.Employees.Services;
using EnterpriseManagement.Application.Features.Projects.Services;
using EnterpriseManagement.Application.Features.Tasks.Services;
using EnterpriseManagement.Application.Features.Users.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseManagement.Application;

/// <summary>Registers everything the Application layer provides.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scans this assembly for AbstractValidator<T> implementations, so a new
        // validator is registered by existing, not by editing this file.
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Scoped: services depend on the scoped DbContext, so a singleton would
        // capture a disposed context on the second request.
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
