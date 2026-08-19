namespace EnterpriseManagement.Application.Common.Models;

/// <summary>
/// The single error shape every failing request returns, whatever went wrong.
/// </summary>
/// <remarks>
/// One shape means a client writes one error handler instead of guessing whether
/// a 404 looks like a 500. The status code is repeated in the body because
/// clients frequently log the payload without the response metadata.
/// </remarks>
public class ApiErrorResponse
{
    public bool Success => false;

    /// <summary>
    /// Safe to show a user. For expected failures this is the domain message;
    /// for unexpected ones it is a fixed generic string, never the exception text.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    /// <summary>
    /// Correlates this response with the server logs. Give this to support and
    /// the exact request can be found, without exposing anything about the fault.
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// Field-level messages for validation failures, keyed by property name.
    /// Null for every other kind of error so clients can branch on its presence.
    /// </summary>
    public IDictionary<string, string[]>? Errors { get; set; }
}
