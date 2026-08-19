using Serilog.Context;

namespace EnterpriseManagement.Api.Middleware;

/// <summary>
/// Gives every request a stable id, echoes it back, and attaches it to every log
/// line written while the request is in flight.
/// </summary>
/// <remarks>
/// <para>
/// Registered first so that everything downstream — including the exception
/// handler — can reference the same id. When a user reports "my request failed",
/// the id from their error response finds every log line for that request, with
/// no guessing from timestamps.
/// </para>
/// <para>
/// An inbound <c>X-Correlation-Id</c> is honoured so a chain of services shares
/// one id. It is bounded and sanitised first: it is attacker-controlled input
/// that ends up in log files, and an unbounded value invites log flooding while
/// newlines invite log forging (injecting fake log lines).
/// </para>
/// </remarks>
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 64;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Items[HeaderName] = correlationId;

        // Set before the response starts. Headers cannot be modified once the
        // first byte of the body is written, so deferring this to the way back
        // out would throw on any endpoint that has begun streaming.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Ambient property for every Serilog event written inside this scope,
        // including from services that know nothing about HTTP.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var incoming))
        {
            var candidate = incoming.ToString();

            if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length <= MaxLength && IsSafe(candidate))
            {
                return candidate;
            }
        }

        // TraceIdentifier is already unique per request and is what ASP.NET Core
        // reports elsewhere, so reusing it keeps the id consistent across the
        // framework's own diagnostics.
        return context.TraceIdentifier;
    }

    /// <summary>
    /// Restricts the value to characters that cannot forge a log entry or break
    /// a header. Rejecting is safe here: we simply fall back to a generated id.
    /// </summary>
    private static bool IsSafe(string value) =>
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ':' or '.');
}
