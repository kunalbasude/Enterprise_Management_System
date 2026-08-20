namespace EnterpriseManagement.Application.Common.Interfaces;

/// <summary>
/// Per-request facts about the caller's connection, for the audit trail.
/// </summary>
/// <remarks>
/// Separate from <see cref="ICurrentUser"/>, which answers "who is acting".
/// This answers "from where, and as part of which request" — different
/// questions, and this one has no meaning outside an HTTP context, so a
/// background job can supply nulls without pretending to have an identity.
/// </remarks>
public interface IRequestContext
{
    /// <summary>
    /// Caller IP, when available.
    /// </summary>
    /// <remarks>
    /// Personal data under GDPR. Recorded because investigating account
    /// compromise genuinely requires it, and for nothing else.
    /// </remarks>
    string? IpAddress { get; }

    /// <summary>
    /// The request's correlation id, so an audit row can be tied to the
    /// application log lines for the same request.
    /// </summary>
    string? CorrelationId { get; }
}
