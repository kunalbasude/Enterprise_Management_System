namespace EnterpriseManagement.Application.Features.Users.Dtos;

/// <summary>A user as it appears in the admin list.</summary>
/// <remarks>
/// Deliberately excludes <c>PasswordHash</c>. A hash is not a secret in the way
/// a password is, but publishing one lets an attacker crack it offline at
/// leisure, so it never leaves the database.
/// </remarks>
public class UserListDto
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = [];

    /// <summary>Linked employee record, when one exists.</summary>
    public int? EmployeeId { get; set; }

    public string? EmployeeCode { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
