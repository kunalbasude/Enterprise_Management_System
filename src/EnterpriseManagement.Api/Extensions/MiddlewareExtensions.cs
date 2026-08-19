using EnterpriseManagement.Api.Middleware;

namespace EnterpriseManagement.Api.Extensions;

/// <summary>
/// Named registration helpers so <c>Program.cs</c> reads as an ordered list of
/// concerns rather than a wall of <c>UseMiddleware&lt;T&gt;</c> calls.
/// </summary>
public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();

    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
