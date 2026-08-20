using System.Reflection;
using EnterpriseManagement.Application.Features.Auth.Services;
using EnterpriseManagement.Application.Features.Departments.Services;
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

        return services;
    }
}
