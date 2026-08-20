using EnterpriseManagement.Application.Common.Models;

namespace EnterpriseManagement.Application.Features.Users.Dtos;

public class UserQueryParameters : QueryParameters
{
    /// <summary>Filter by role name, e.g. ?role=ADMIN.</summary>
    public string? Role { get; set; }

    /// <summary>Filter by active state. Null returns both.</summary>
    public bool? IsActive { get; set; }

    public static readonly IReadOnlyList<string> SortableFields =
        ["email", "fullName", "createdAt", "lastLoginAt"];
}
