using EnterpriseManagement.Api.Authentication;
using EnterpriseManagement.Api.Extensions;
using EnterpriseManagement.Api.Filters;
using EnterpriseManagement.Application;
using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Infrastructure;
using Serilog;
using Serilog.Events;

// Bootstrap logger: active before the host is built, so a failure in
// configuration or DI registration is logged rather than vanishing into a
// silent crash. Replaced below once configuration is available.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "EnterpriseManagement.Api"));

    // Composition root: one call per layer.
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddAuthorization();

    // ICurrentUser reads claims off the request, so it needs the accessor and
    // must be scoped to the request.
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();

    builder.Services.AddControllers(options =>
    {
        // Global: every action with a registered validator is validated, with
        // nothing to remember per endpoint.
        options.Filters.Add<ValidationFilter>();
    });

    // MVC's built-in ModelState 400 would bypass the exception middleware and
    // return a different error shape. Suppressed so every failure, validation
    // included, comes out of one place.
    builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerWithJwt();

    var app = builder.Build();

    // ---------------------------------------------------------------------
    // Middleware pipeline. Order is behaviour, not preference.
    //
    // 1. Correlation id  - must be first so every log line below carries it.
    // 2. Request logging - one structured line per request, with the id.
    // 3. Exception handling - inside logging so failures are still logged with
    //    their id, outside the endpoint so it catches anything thrown there.
    // 4. HTTPS redirect  - before auth: no point authenticating a request that
    //    is about to be redirected.
    // 5. Authentication  - builds the ClaimsPrincipal.
    // 6. Authorization   - evaluates policies against it. Cannot precede 5.
    // 7. Endpoints       - the controller finally runs.
    // ---------------------------------------------------------------------

    app.UseCorrelationId();

    app.UseSerilogRequestLogging(options =>
    {
        // Health checks and successful Swagger asset requests are noise at
        // Information; a real failure still surfaces at Error.
        options.GetLevel = (httpContext, elapsed, ex) =>
            ex is not null || httpContext.Response.StatusCode >= 500
                ? LogEventLevel.Error
                : httpContext.Request.Path.StartsWithSegments("/swagger")
                    ? LogEventLevel.Verbose
                    : LogEventLevel.Information;

        // NOTE: request headers are deliberately NOT enriched here. The
        // Authorization header would be captured verbatim, writing bearer
        // tokens into the log files.
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    app.UseExceptionHandling();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    // Authentication must precede authorization: a policy cannot be evaluated
    // against a ClaimsPrincipal that has not been built yet.
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    Log.Information("Starting Enterprise Management API in {Environment}", app.Environment.EnvironmentName);

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is thrown by the EF Core design-time tooling when it
    // builds the host to read configuration. It is expected, not a failure.
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    // Serilog buffers; without this a crash can lose the very log lines that
    // explain it.
    Log.CloseAndFlush();
}

/// <summary>
/// Exposed so integration tests can reference this entry point with
/// WebApplicationFactory. Top-level statements otherwise generate an internal class.
/// </summary>
public partial class Program;
