using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Application.Features.AuditLogs.Dtos;

public class AuditLogDto
{
    public int Id { get; set; }

    /// <summary>Null when the actor's account has since been deleted.</summary>
    public int? UserId { get; set; }

    /// <summary>
    /// The actor's email as it was at the time.
    /// </summary>
    /// <remarks>
    /// Read from the denormalised column, not a join. That is what keeps the
    /// trail readable after the user is renamed or removed — the whole point of
    /// copying it at write time.
    /// </remarks>
    public string? UserEmail { get; set; }

    public AuditAction Action { get; set; }

    public string ActionName => Action.ToString();

    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    /// <summary>Sanitised JSON detail. Never contains credentials.</summary>
    public string? Metadata { get; set; }

    public string? IpAddress { get; set; }

    /// <summary>Ties this entry to the application log lines for the same request.</summary>
    public string? CorrelationId { get; set; }

    public DateTime CreatedAt { get; set; }
}
