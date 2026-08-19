using System.Text.Json;
using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Domain.Exceptions;
using ApplicationValidationException = EnterpriseManagement.Application.Common.Exceptions.ValidationException;

namespace EnterpriseManagement.Api.Middleware;

/// <summary>
/// Converts any unhandled exception into the single <see cref="ApiErrorResponse"/>
/// shape, with the right status code and no internal detail leaked.
/// </summary>
/// <remarks>
/// <para>
/// Centralising this is what lets services throw a meaningful exception and
/// return nothing else. Without it every controller action needs its own
/// try/catch, and the first one somebody forgets returns an HTML error page and
/// a stack trace to the caller.
/// </para>
/// <para>
/// <b>Placement:</b> registered inside the request logger so a failure still
/// produces a log line carrying its correlation id, and outside the endpoint so
/// it catches anything a controller or service throws.
/// </para>
/// </remarks>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items[CorrelationIdMiddleware.HeaderName] as string
            ?? context.TraceIdentifier;

        var (statusCode, message, errors) = Map(exception);

        // Expected failures are the application working as designed, so they log
        // at Warning without a stack trace. Anything unmapped is a bug: log the
        // full exception at Error so it is actually investigable.
        if (statusCode >= 500)
        {
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}. CorrelationId {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                correlationId);
        }
        else
        {
            _logger.LogWarning(
                "Request failed with {StatusCode} for {Method} {Path}: {Reason}. CorrelationId {CorrelationId}",
                statusCode,
                context.Request.Method,
                context.Request.Path,
                message,
                correlationId);
        }

        // If the response has already begun there is nothing safe to do: status
        // and headers are on the wire and rewriting the body would produce
        // malformed output. Log it and let the connection fail.
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                "Response already started; cannot write error body. CorrelationId {CorrelationId}",
                correlationId);
            return;
        }

        var response = new ApiErrorResponse
        {
            Message = message,
            StatusCode = statusCode,
            TraceId = correlationId,
            Errors = errors
        };

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, SerializerOptions));
    }

    private (int StatusCode, string Message, IDictionary<string, string[]>? Errors) Map(Exception exception) =>
        exception switch
        {
            // 400: the request itself is malformed or fails field validation.
            ApplicationValidationException validation =>
                (StatusCodes.Status400BadRequest, validation.Message, validation.Errors),

            // 401: credentials missing or invalid. Distinct from 403, which
            // means the identity is known and simply not permitted.
            UnauthorizedException unauthorized =>
                (StatusCodes.Status401Unauthorized, unauthorized.Message, null),

            // 404: the target does not exist. The domain message names the type
            // and id only — it never echoes arbitrary caller input.
            NotFoundException notFound =>
                (StatusCodes.Status404NotFound, notFound.Message, null),

            // 409: the request is valid but collides with existing state.
            ConflictException conflict =>
                (StatusCodes.Status409Conflict, conflict.Message, null),

            // 422: understood perfectly, and the domain refuses. Distinct from
            // 400 because retrying the identical payload can never succeed.
            BusinessRuleViolationException businessRule =>
                (StatusCodes.Status422UnprocessableEntity, businessRule.Message, null),

            // 403 is produced by the authorization middleware, not thrown here.

            // The client gave up and disconnected. Not a server fault, and it
            // must not pollute the error rate.
            OperationCanceledException =>
                (StatusCodes.Status499ClientClosedRequest, "Request was cancelled.", null),

            // Anything else is a bug. The caller learns nothing beyond the trace
            // id: exception text routinely contains table names, file paths and
            // connection details, all of which help an attacker map the system.
            _ => (StatusCodes.Status500InternalServerError, BuildServerErrorMessage(exception), null)
        };

    /// <summary>
    /// Outside Development the message is a fixed string. The real detail is in
    /// the logs, findable by trace id.
    /// </summary>
    private string BuildServerErrorMessage(Exception exception) =>
        _environment.IsDevelopment()
            ? $"{exception.GetType().Name}: {exception.Message}"
            : "An unexpected error occurred. Please contact support with the trace id.";
}
