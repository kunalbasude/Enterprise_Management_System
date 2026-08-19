namespace EnterpriseManagement.Domain.Common;

/// <summary>
/// Base for every persisted entity.
/// </summary>
/// <remarks>
/// <para>
/// <c>int</c> surrogate keys are used rather than <c>Guid</c>: they are half the
/// width, keep B-tree indexes compact, and cluster naturally on insert. Random
/// GUIDs fragment the index and are only worth it when ids must be generated
/// client-side or merged across databases — neither applies here.
/// </para>
/// <para>
/// The trade-off: sequential ids are guessable and leak row counts, so every
/// endpoint must still authorise by ownership rather than trusting the id.
/// </para>
/// </remarks>
public abstract class BaseEntity
{
    public int Id { get; set; }
}
