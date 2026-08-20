using System.Linq.Expressions;
using EnterpriseManagement.Application.Common.Extensions;
using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Users.Dtos;
using EnterpriseManagement.Domain.Common;
using EnterpriseManagement.Domain.Entities;
using EnterpriseManagement.Domain.Enums;
using EnterpriseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnterpriseManagement.Application.Features.Users.Services;

public class UserService : IUserService
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly IAuditService _auditService;
    private readonly ILogger<UserService> _logger;

    private static readonly Dictionary<string, Expression<Func<UserListDto, object>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["email"] = u => u.Email,
            ["fullName"] = u => u.FullName,
            ["createdAt"] = u => u.CreatedAt,
            // Nullable: users who have never logged in sort together. Coalescing
            // keeps the ordering total instead of provider-defined.
            ["lastLoginAt"] = u => u.LastLoginAt ?? DateTime.MinValue
        };

    public UserService(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        IAuditService auditService,
        ILogger<UserService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<PagedResult<UserListDto>> GetAsync(
        UserQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var term = parameters.Search.Trim().ToLowerInvariant();

            query = query.Where(u =>
                u.Email.ToLower().Contains(term) ||
                u.FullName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Role))
        {
            var role = parameters.Role.Trim().ToUpperInvariant();

            // Translates to an EXISTS subquery, not a join, so a user with two
            // roles is not returned twice.
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name == role));
        }

        if (parameters.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == parameters.IsActive.Value);
        }

        var projected = query.Select(u => new UserListDto
        {
            Id = u.Id,
            Email = u.Email,
            FullName = u.FullName,
            IsActive = u.IsActive,
            Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList(),
            EmployeeId = u.Employee != null ? u.Employee.Id : null,
            EmployeeCode = u.Employee != null ? u.Employee.EmployeeCode : null,
            LastLoginAt = u.LastLoginAt,
            CreatedAt = u.CreatedAt
        });

        var sorted = projected.ApplySorting(
            parameters.SortBy,
            parameters.IsDescending,
            SortMap,
            defaultSort: u => u.Email,
            tiebreaker: u => u.Id);

        return await sorted.ToPagedResultAsync(parameters.Page, parameters.PageSize, cancellationToken);
    }

    public async Task<UserListDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await ProjectSingle(id).FirstOrDefaultAsync(cancellationToken);

        return user ?? throw new NotFoundException(nameof(User), id);
    }

    public async Task<UserListDto> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflictException("An account with this email address already exists.");
        }

        var roles = await ResolveRolesAsync(request.Roles, cancellationToken);

        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            IsActive = true
        };

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var role in roles)
        {
            user.UserRoles.Add(new UserRole { Role = role, AssignedAt = now });
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        // Logged because granting roles is a security event. The password is not.
        _logger.LogInformation(
            "Admin {ActorId} created user {UserId} with roles {Roles}",
            _currentUser.UserId, user.Id, string.Join(",", roles.Select(r => r.Name)));

        await _auditService.LogAsync(
            AuditAction.Created,
            nameof(User),
            user.Id,
            new { user.Email, Roles = roles.Select(r => r.Name), Source = "admin-created" },
            cancellationToken: cancellationToken);

        return await GetByIdAsync(user.Id, cancellationToken);
    }

    public async Task<UserListDto> UpdateAsync(
        int id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        var email = request.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AsNoTracking()
                .AnyAsync(u => u.Id != id && u.Email == email, cancellationToken))
        {
            throw new ConflictException("An account with this email address already exists.");
        }

        // Deactivating an account is functionally identical to removing its
        // access, so it must pass the same last-admin check as role removal.
        if (!request.IsActive && user.IsActive)
        {
            await GuardLastActiveAdminAsync(user, removingAdmin: IsAdmin(user), cancellationToken);
        }

        user.Email = email;
        user.FullName = request.FullName.Trim();
        user.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin {ActorId} updated user {UserId}", _currentUser.UserId, id);

        await _auditService.LogAsync(
            AuditAction.Updated,
            nameof(User),
            id,
            new { user.Email, user.FullName, user.IsActive },
            cancellationToken: cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<UserListDto> UpdateRolesAsync(
        int id,
        UpdateUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        var requested = await ResolveRolesAsync(request.Roles, cancellationToken);

        var losingAdmin = IsAdmin(user) && requested.All(r => r.Name != RoleNames.Admin);

        if (losingAdmin)
        {
            await GuardLastActiveAdminAsync(user, removingAdmin: true, cancellationToken);
        }

        // Captured before the collection is cleared, so the audit entry can
        // record what the roles WERE as well as what they became.
        var previousRoles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Replace wholesale: the request states the complete desired set, which
        // is simpler to reason about than a diff and makes the endpoint
        // idempotent.
        user.UserRoles.Clear();

        foreach (var role in requested)
        {
            user.UserRoles.Add(new UserRole { Role = role, AssignedAt = now });
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Admin {ActorId} changed roles for user {UserId} to {Roles}",
            _currentUser.UserId, id, string.Join(",", requested.Select(r => r.Name)));

        // Records both sides of the change. "Was admin, now employee" is the
        // question an investigation actually asks; the new value alone does not
        // answer it.
        await _auditService.LogAsync(
            AuditAction.Updated,
            "UserRoles",
            id,
            new { PreviousRoles = previousRoles, NewRoles = requested.Select(r => r.Name) },
            cancellationToken: cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task ResetPasswordAsync(
        int id,
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);

        await _context.SaveChangesAsync(cancellationToken);

        // A security event worth surfacing at Warning: an administrative reset
        // is indistinguishable from an account takeover if nobody is watching.
        _logger.LogWarning("Admin {ActorId} reset the password for user {UserId}", _currentUser.UserId, id);

        // Records THAT a reset happened, never the new password. An
        // administrative reset is indistinguishable from an account takeover
        // unless it leaves a trail.
        await _auditService.LogAsync(
            AuditAction.Updated,
            "UserPassword",
            id,
            new { Method = "admin-reset" },
            cancellationToken: cancellationToken);
    }

    public async Task ChangeOwnPasswordAsync(
        int userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        // Re-authenticating here is what prevents an unattended session or a
        // stolen token from becoming permanent account takeover.
        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            _logger.LogWarning("Failed password change for user {UserId}: current password incorrect", userId);
            throw new UnauthorizedException("Current password is incorrect.");
        }

        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            throw new BusinessRuleViolationException("The new password must differ from the current one.");
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} changed their own password", userId);

        await _auditService.LogAsync(
            AuditAction.Updated,
            "UserPassword",
            userId,
            new { Method = "self-service" },
            userIdOverride: userId,
            cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        if (_currentUser.UserId == id)
        {
            throw new BusinessRuleViolationException(
                "You cannot delete your own account. Ask another administrator to do it.");
        }

        await GuardLastActiveAdminAsync(user, removingAdmin: IsAdmin(user), cancellationToken);

        // The employee record survives: employees.user_id is SetNull, so history
        // stays attributable. Audit rows likewise keep their denormalised email.
        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Admin {ActorId} deleted user {UserId}", _currentUser.UserId, id);

        // Written after the delete, and survives it: audit_logs.user_id is
        // SET NULL, while UserEmail is denormalised so the row stays readable.
        await _auditService.LogAsync(
            AuditAction.Deleted,
            nameof(User),
            id,
            new { DeletedEmail = user.Email, DeletedRoles = user.UserRoles.Select(ur => ur.Role.Name) },
            cancellationToken: cancellationToken);
    }

    private IQueryable<UserListDto> ProjectSingle(int id) =>
        _context.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserListDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                IsActive = u.IsActive,
                Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList(),
                EmployeeId = u.Employee != null ? u.Employee.Id : null,
                EmployeeCode = u.Employee != null ? u.Employee.EmployeeCode : null,
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt
            });

    private static bool IsAdmin(User user) =>
        user.UserRoles.Any(ur => ur.Role.Name == RoleNames.Admin);

    private async Task<List<Role>> ResolveRolesAsync(
        IEnumerable<string> roleNames,
        CancellationToken cancellationToken)
    {
        var requested = roleNames
            .Select(r => r.Trim().ToUpperInvariant())
            .Where(r => r.Length > 0)
            .Distinct()
            .ToList();

        if (requested.Count == 0)
        {
            throw new BusinessRuleViolationException("At least one role must be assigned.");
        }

        var roles = await _context.Roles
            .Where(r => requested.Contains(r.Name))
            .ToListAsync(cancellationToken);

        // Reject unknown names rather than silently dropping them: a typo that
        // quietly grants fewer roles than intended is worse than an error.
        var unknown = requested.Except(roles.Select(r => r.Name)).ToList();

        if (unknown.Count > 0)
        {
            throw new BusinessRuleViolationException(
                $"Unknown role(s): {string.Join(", ", unknown)}. " +
                $"Valid roles are: {string.Join(", ", RoleNames.All)}.");
        }

        return roles;
    }

    /// <summary>
    /// Refuses any change that would leave the system with no active
    /// administrator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Covers four routes to the same disaster: deleting the last admin,
    /// deactivating them, removing their ADMIN role, and an admin doing any of
    /// those to themselves. Enforcing the single invariant "at least one active
    /// ADMIN must remain" is more robust than four separate special cases,
    /// because a fifth route added later is covered automatically.
    /// </para>
    /// <para>
    /// Without it, recovery means editing the database by hand.
    /// </para>
    /// </remarks>
    private async Task GuardLastActiveAdminAsync(
        User user,
        bool removingAdmin,
        CancellationToken cancellationToken)
    {
        if (!removingAdmin)
        {
            return;
        }

        var otherActiveAdmins = await _context.Users
            .AsNoTracking()
            .CountAsync(
                u => u.Id != user.Id
                     && u.IsActive
                     && u.UserRoles.Any(ur => ur.Role.Name == RoleNames.Admin),
                cancellationToken);

        if (otherActiveAdmins == 0)
        {
            throw new BusinessRuleViolationException(
                "This is the last active administrator. Promote another user to ADMIN first, " +
                "otherwise nobody would be able to administer the system.");
        }
    }
}
