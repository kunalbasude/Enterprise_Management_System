using EnterpriseManagement.Api.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace EnterpriseManagement.Tests.Middleware;

/// <summary>
/// Exercised through a real <see cref="TestServer"/> rather than a
/// <c>DefaultHttpContext</c>.
/// </summary>
/// <remarks>
/// The middleware sets the response header from a <c>Response.OnStarting</c>
/// callback, which is correct — headers cannot be written once the body has
/// started. But <c>DefaultHttpContext</c> never fires those callbacks, so a test
/// built on it reports a null header and looks like a middleware bug. A real
/// host runs the callback exactly as Kestrel would, which is the behaviour worth
/// asserting anyway: that the header actually reaches the client.
/// </remarks>
public class CorrelationIdMiddlewareTests : IAsyncLifetime
{
    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .Configure(app =>
                {
                    app.UseMiddleware<CorrelationIdMiddleware>();

                    // Echo back what the middleware stored, so both the header
                    // and HttpContext.Items are observable from one request.
                    app.Run(context =>
                    {
                        var stored = context.Items[CorrelationIdMiddleware.HeaderName] as string;
                        return context.Response.WriteAsync(stored ?? "<none>");
                    });
                }))
            .StartAsync();

        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    private async Task<(string Header, string Body)> GetAsync(string? incoming)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");

        if (incoming is not null)
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, incoming);
        }

        using var response = await _client.SendAsync(request);

        var header = response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values)
            ? string.Join(",", values)
            : string.Empty;

        return (header, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Generates_an_id_when_none_is_supplied()
    {
        var (header, body) = await GetAsync(null);

        Assert.False(string.IsNullOrWhiteSpace(header));
        Assert.Equal(header, body);   // response header and log context agree
    }

    [Fact]
    public async Task Honours_a_valid_inbound_id_so_it_flows_across_services()
    {
        var (header, body) = await GetAsync("abc-123_DEF.456");

        Assert.Equal("abc-123_DEF.456", header);
        Assert.Equal("abc-123_DEF.456", body);
    }

    [Theory]
    // Newlines would let a caller forge fake entries in the log file.
    [InlineData("evil\nINFO fake log line")]
    [InlineData("evil\r\n[ERROR] injected")]
    // Unsafe wherever logs are later rendered as HTML.
    [InlineData("<script>alert(1)</script>")]
    [InlineData("has spaces")]
    public async Task Rejects_unsafe_inbound_ids_and_falls_back(string malicious)
    {
        var (header, _) = await GetAsync(malicious);

        Assert.NotEqual(malicious, header);
        Assert.DoesNotContain('\n', header);
        Assert.DoesNotContain('\r', header);
        Assert.DoesNotContain('<', header);
    }

    [Fact]
    public async Task Rejects_an_over_long_inbound_id()
    {
        // Unbounded input is written to every log line for the request, which
        // makes it a cheap log-flooding vector.
        var tooLong = new string('a', 65);

        var (header, _) = await GetAsync(tooLong);

        Assert.NotEqual(tooLong, header);
    }

    [Fact]
    public async Task Accepts_an_id_at_exactly_the_length_limit()
    {
        var atLimit = new string('a', 64);

        var (header, _) = await GetAsync(atLimit);

        Assert.Equal(atLimit, header);
    }
}
