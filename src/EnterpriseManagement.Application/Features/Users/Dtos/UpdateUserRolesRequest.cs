namespace EnterpriseManagement.Application.Features.Users.Dtos;

/// <summary>Replaces a user's roles with exactly this set.</summary>
public class UpdateUserRolesRequest
{
    public List<string> Roles { get; set; } = [];
}
