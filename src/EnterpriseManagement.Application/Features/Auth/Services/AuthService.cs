using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Application.Features.Auth.Dtos;
using EnterpriseManagement.Domain.Common;
using EnterpriseManagement.Domain.Entities;
using EnterpriseManagement.Domain.Enums;
using EnterpriseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnterpriseManagement.Application.Features.Auth.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        TimeProvider timeProvider,
        IAuditService auditService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _timeProvider = timeProvider;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        // Normalised once, here, so the unique index on users.email is a plain
        // btree rather than needing a functional lower(email) index, and so
        // "Ada@x.com" and "ada@x.com" cannot both be registered.
        var email = request.Email.Trim().ToLowerInvariant();

        var emailTaken = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == email, cancellationToken);

        if (emailTaken)
        {
            // NOTE: this does disclose that an address is registered. That is a
            // deliberate trade-off on a registration endpoint, where the
            // alternative (pretending to succeed) makes the form unusable. The
            // login endpoint below does NOT leak the same information.
            throw new ConflictException("An account with this email address already exists.");
        }

        var employeeRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == RoleNames.Employee, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Seeded role '{RoleNames.Employee}' is missing. The database is not correctly migrated.");

        var user = new User
        {
            Email = email,
            // Hashed before the entity is ever added to the change tracker, so
            // no plaintext exists on an object that could be serialised or logged.
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            IsActive = true
        };

        // Self-registration always grants the least-privileged role. Elevation is
        // an administrative act, never something a caller can request.
        user.UserRoles.Add(new UserRole
        {
            Role = employeeRole,
            AssignedAt = _timeProvider.GetUtcNow().UtcDateTime
        });

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registered new user {UserId} with role {Role}", user.Id, RoleNames.Employee);

        // Only the email and granted role are recorded. The submitted password
        // is never handed to the audit service at all.
        await _auditService.LogAsync(
            AuditAction.Created,
            nameof(User),
            user.Id,
            new { user.Email, Roles = new[] { RoleNames.Employee }, Source = "self-registration" },
            userIdOverride: user.Id,
            userEmailOverride: user.Email,
            cancellationToken: cancellationToken);

        return BuildAuthResponse(user, [RoleNames.Employee], employeeId: null);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // Tracked (no AsNoTracking) because LastLoginAt is updated below.
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // One identical failure for "no such user", "wrong password" and
        // "disabled account". Distinguishing them turns the login endpoint into
        // an account enumeration oracle: an attacker learns which addresses are
        // registered without ever guessing a password.
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            // Logged with the email because a failed login is a security event
            // worth investigating. The submitted PASSWORD is never logged.
            _logger.LogWarning(
                "Failed login attempt for {Email} from {IpAddress}",
                email,
                ipAddress ?? "unknown");

            // Recorded although nobody is authenticated: a burst of these across
            // many addresses is what credential stuffing looks like, and it is
            // invisible without an audit row.
            await _auditService.LogAsync(
                AuditAction.LoginFailed,
                nameof(User),
                user?.Id,
                new { AttemptedEmail = email, Reason = user is null ? "unknown account" : "incorrect password" },
                userIdOverride: user?.Id,
                userEmailOverride: email,
                cancellationToken: cancellationToken);

            throw new UnauthorizedException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt on disabled account {UserId}", user.Id);

            await _auditService.LogAsync(
                AuditAction.LoginFailed,
                nameof(User),
                user.Id,
                new { AttemptedEmail = email, Reason = "account disabled" },
                userIdOverride: user.Id,
                userEmailOverride: email,
                cancellationToken: cancellationToken);

            throw new UnauthorizedException("Invalid email or password.");
        }

        user.LastLoginAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync(cancellationToken);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

        _logger.LogInformation("User {UserId} logged in from {IpAddress}", user.Id, ipAddress ?? "unknown");

        // The issued token is NOT recorded. An audit row containing a valid JWT
        // would hand anyone with log access a working credential.
        await _auditService.LogAsync(
            AuditAction.Login,
            nameof(User),
            user.Id,
            new { Roles = roles },
            userIdOverride: user.Id,
            userEmailOverride: user.Email,
            cancellationToken: cancellationToken);

        return BuildAuthResponse(user, roles, user.Employee?.Id);
    }

    public async Task<UserDto> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        // Projected with Select rather than loading the entity: it fetches only
        // the columns the DTO needs and never materialises PasswordHash at all.
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                IsActive = u.IsActive,
                Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList(),
                EmployeeId = u.Employee != null ? u.Employee.Id : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        return user ?? throw new NotFoundException("User", userId);
    }

    private AuthResponse BuildAuthResponse(User user, IReadOnlyList<string> roles, int? employeeId)
    {
        var (token, expiresAt) = _tokenGenerator.GenerateToken(user, roles, employeeId);

        return new AuthResponse
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                IsActive = user.IsActive,
                Roles = roles,
                EmployeeId = employeeId
            }
        };
    }
}
