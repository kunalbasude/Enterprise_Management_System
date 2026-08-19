namespace EnterpriseManagement.Domain.Common;

/// <summary>
/// Marks an entity whose creation and modification timestamps are maintained
/// automatically by the persistence layer's SaveChanges override, so no service
/// has to remember to set them.
/// </summary>
/// <remarks>
/// All timestamps are UTC. Npgsql maps <see cref="DateTime"/> to
/// <c>timestamptz</c> and rejects any value whose Kind is not Utc, which turns
/// the classic "stored local time by accident" bug into a runtime failure.
/// </remarks>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}
