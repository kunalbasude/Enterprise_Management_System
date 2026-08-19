using System.Net;
using System.Text.Json;
using EnterpriseManagement.Api.Middleware;
using EnterpriseManagement.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ApplicationValidationException = EnterpriseManagement.Application.Common.Exceptions.ValidationException;

namespace EnterpriseManagement.Tests.Middleware;

/// <summary>
/// Drives the real middleware with a delegate that throws, so the mapping from
/// exception type to status code and body is verified rather than assumed.
/// </summary>
public class ExceptionHandlingMiddlewareTests
{
    private sealed class StubEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static async Task<(int StatusCode, JsonElement Body)> InvokeAsync(
        Exception thrown,
        string environmentName = "Production")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/employees/999";
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.HeaderName] = "test-correlation-id";

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw thrown,
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            new StubEnvironment { EnvironmentName = environmentName });

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();

        return (context.Response.StatusCode, JsonDocument.Parse(json).RootElement);
    }

    [Fact]
    public async Task NotFoundException_maps_to_404_with_its_message()
    {
        var (status, body) = await InvokeAsync(new NotFoundException("Employee", 999));

        Assert.Equal((int)HttpStatusCode.NotFound, status);
        Assert.Equal(404, body.GetProperty("statusCode").GetInt32());
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("Employee with id 999 was not found.", body.GetProperty("message").GetString());
        Assert.Equal("test-correlation-id", body.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task ConflictException_maps_to_409()
    {
        var (status, body) = await InvokeAsync(new ConflictException("Email is already registered."));

        Assert.Equal((int)HttpStatusCode.Conflict, status);
        Assert.Equal("Email is already registered.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task BusinessRuleViolation_maps_to_422_not_400()
    {
        // 422 rather than 400 tells the client that retrying the identical
        // payload can never succeed. That distinction is the whole point.
        var (status, _) = await InvokeAsync(
            new BusinessRuleViolationException("A cancelled task cannot be reopened."));

        Assert.Equal(422, status);
    }

    [Fact]
    public async Task ValidationException_maps_to_400_with_per_field_errors()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Email"] = ["Email is required.", "Email must be a valid address."],
            ["Password"] = ["Password must be at least 8 characters."]
        };

        var (status, body) = await InvokeAsync(new ApplicationValidationException(errors));

        Assert.Equal(400, status);

        var returned = body.GetProperty("errors");
        Assert.Equal(2, returned.GetProperty("Email").GetArrayLength());
        Assert.Equal("Password must be at least 8 characters.",
            returned.GetProperty("Password")[0].GetString());
    }

    [Fact]
    public async Task Unmapped_exception_maps_to_500()
    {
        var (status, _) = await InvokeAsync(new InvalidOperationException("boom"));

        Assert.Equal(500, status);
    }

    [Fact]
    public async Task Production_500_leaks_no_exception_detail()
    {
        // The security-relevant test. Exception text routinely contains table
        // names, file paths and connection details.
        var (_, body) = await InvokeAsync(
            new InvalidOperationException("Npgsql connection to 10.0.0.5 failed for user admin"),
            Environments.Production);

        var message = body.GetProperty("message").GetString()!;

        Assert.DoesNotContain("Npgsql", message);
        Assert.DoesNotContain("10.0.0.5", message);
        Assert.DoesNotContain("admin", message);
        Assert.Equal("An unexpected error occurred. Please contact support with the trace id.", message);
        Assert.False(body.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public async Task Development_500_includes_detail_to_aid_debugging()
    {
        var (_, body) = await InvokeAsync(
            new InvalidOperationException("boom"),
            Environments.Development);

        Assert.Contains("boom", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Cancelled_request_is_not_reported_as_a_server_error()
    {
        // A client disconnecting is not a server fault and must not inflate the
        // 5xx error rate that alerting is based on.
        var (status, _) = await InvokeAsync(new OperationCanceledException());

        Assert.Equal(499, status);
        Assert.True(status < 500);
    }
}
