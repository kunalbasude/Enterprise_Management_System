using EnterpriseManagement.Api.Middleware;
using EnterpriseManagement.Application.Common.Interfaces;

namespace EnterpriseManagement.Api.Authentication;

/// <summary>
/// Reads connection facts off the current HTTP request.
/// </summary>
/// <remarks>
/// Implemented in the Api layer because it is the only layer permitted to know
/// about <c>HttpContext</c>. Returns null outside a request, so a background job
/// records no IP rather than a misleading one.
/// </remarks>
public class RequestContext : IRequestContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequestContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// The caller's IP as the server sees it.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT read from X-Forwarded-For. That header is
    /// client-supplied and trivially spoofed, so trusting it would let an
    /// attacker write any address they like into the audit trail. Behind a
    /// reverse proxy the correct fix is ForwardedHeadersOptions with the proxy
    /// explicitly listed as a known network, which is a deployment decision
    /// rather than something to assume here.
    /// </remarks>
    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? CorrelationId =>
        _httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.HeaderName] as string;
}
