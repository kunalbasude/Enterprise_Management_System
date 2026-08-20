using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Domain.Common;
using EnterpriseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EnterpriseManagement.Infrastructure.Persistence.Seed;

/// <summary>
/// Creates the initial administrator account, if one is configured.
/// </summary>
/// <remarks>
/// <para>
/// The bootstrap problem: only an ADMIN can grant the ADMIN role, and
/// self-registration deliberately cannot. So the first admin has to come from
/// somewhere outside the API.
/// </para>
/// <para>
/// Credentials come from configuration — user-secrets locally,
/// <c>Seed__AdminEmail</c> and <c>Seed__AdminPassword</c> in Docker. Nothing is
/// hardcoded. If they are not configured the seeder logs and does nothing,
/// which is the correct behaviour in a production deployment where the admin
/// already exists.
/// </para>
/// <para>
/// Idempotent: safe to run on every startup. It never overwrites an existing
/// account, so a rotated production password is not silently reset back to the
/// configured one on the next deploy.
/// </para>
/// </remarks>
public class DatabaseSeeder
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        TimeProvider timeProvider,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var email = _configuration["Seed:AdminEmail"];
        var password = _configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation(
                "Seed:AdminEmail / Seed:AdminPassword are not configured; skipping admin seeding.");
            return;
        }

        email = email.Trim().ToLowerInvariant();

        if (await _context.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            _logger.LogInformation("Admin account {Email} already exists; nothing to seed.", email);
            return;
        }

        var adminRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == RoleNames.Admin, cancellationToken);

        if (adminRole is null)
        {
            _logger.LogError(
                "Role {Role} is missing. Run migrations before seeding.", RoleNames.Admin);
            return;
        }

        var admin = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(password),
            FullName = "System Administrator",
            IsActive = true
        };

        admin.UserRoles.Add(new UserRole
        {
            Role = adminRole,
            AssignedAt = _timeProvider.GetUtcNow().UtcDateTime
        });

        _context.Users.Add(admin);
        await _context.SaveChangesAsync(cancellationToken);

        // The email identifies which account was created. The password is never
        // logged, even at seeding time.
        _logger.LogInformation("Seeded administrator account {Email}", email);
    }
}
