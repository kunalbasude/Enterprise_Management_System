using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Infrastructure.Identity;
using EnterpriseManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EnterpriseManagement.Infrastructure;

/// <summary>
/// Registers everything this layer provides, so <c>Program.cs</c> composes the
/// application with one call per layer instead of knowing about EF Core,
/// connection strings or provider packages.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured. Set it via " +
                "user-secrets for local development or the " +
                "ConnectionStrings__DefaultConnection environment variable.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                // Migrations live in this assembly, not the startup project, so
                // the schema travels with the persistence layer that owns it.
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);

                // Retry transient failures (dropped connection, restarting server)
                // with exponential backoff. Cheap resilience for a containerised
                // database that may not be ready the instant the API starts.
                npgsql.EnableRetryOnFailure(maxRetryCount: 3, TimeSpan.FromSeconds(5), null);
            });

            // PostgreSQL folds unquoted identifiers to lower case, so a PascalCase
            // column has to be quoted in every hand-written query and psql session.
            // Mapping to snake_case keeps the database idiomatic and readable.
            options.UseSnakeCaseNamingConvention();
        });

        // Application depends on the interface; this is the only place the
        // concrete AppDbContext is bound to it. Resolved from the same scoped
        // instance so both share one change tracker and one transaction.
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // The .NET 8 clock abstraction. Registering the real one here lets tests
        // substitute a fake without a custom IDateTime interface.
        services.AddSingleton(TimeProvider.System);

        // Bind the Jwt section and validate it at startup rather than on first
        // use, so a misconfigured deployment fails to boot instead of failing
        // every login in production.
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();

        // Stateless and thread-safe, so singletons are correct and avoid
        // re-allocating per request.
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
