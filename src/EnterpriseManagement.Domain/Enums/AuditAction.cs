namespace EnterpriseManagement.Domain.Enums;

/// <summary>
/// The set of actions worth recording in the audit trail.
/// </summary>
/// <remarks>
/// A closed enum rather than a free-text string: it keeps the column narrow,
/// makes filtering exact, and stops the same action being written three
/// different ways ("Login", "login", "USER_LOGIN") over the life of the app.
/// </remarks>
public enum AuditAction
{
    Login = 0,
    LoginFailed = 1,
    Logout = 2,
    Created = 3,
    Updated = 4,
    Deleted = 5,
    StatusChanged = 6,
    Assigned = 7,
    Unassigned = 8
}
