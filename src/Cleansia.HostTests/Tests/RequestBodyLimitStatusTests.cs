using System.Net;
using System.Text;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// What a client actually receives when a request body crosses Kestrel's <c>MaxRequestBodySize</c> on
/// THIS pipeline — the empirical precondition of any host-wide intake ceiling, since a ceiling whose
/// refusal arrives as a 500 is a ceiling that reports every rejected upload as a server fault.
///
/// <para>The doubt worth settling was the exception handler. The read that trips the limit is the
/// request-logging middleware's, and <c>CleansiaStartupBase.Configure</c> registers that middleware at
/// the hop BEFORE <c>UseExceptionHandler</c> — so it is upstream, the handler never sees the exception,
/// and Kestrel maps it to the status it carries. <c>RequestBodyLimitPipelineOrderTests</c> is the other
/// half: it shows the same read moved downstream of the handler is answered 500, which is what makes
/// this 413 a fact about the ORDERING rather than a fact about Kestrel alone.</para>
///
/// <para>The under-limit case is the control: same route, same anonymous caller, only the size differs.
/// Its 401 also dates the read — the body is consumed BEFORE authentication, so provoking the refusal
/// costs a caller no credentials.</para>
/// </summary>
[Collection(HostTestCollection.Name)]
public sealed class RequestBodyLimitStatusTests(HostTestPostgresFixture db) : IAsyncLifetime
{
    private const long BodyLimitBytes = 4 * 1024;
    private const string Route = "/api/Order/SavePhotos";

    private KestrelPartnerHost _host = default!;

    public async Task InitializeAsync() =>
        _host = await KestrelPartnerHost.StartAsync(db.ConnectionString, BodyLimitBytes);

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Body_over_the_limit_reaches_the_client_as_413_with_no_body()
    {
        var response = await _host.CreateClient().PostAsync(Route, JsonOf(BodyLimitBytes * 4));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Body_under_the_limit_on_the_same_route_is_answered_by_the_auth_layer()
    {
        var response = await _host.CreateClient().PostAsync(Route, JsonOf(512));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A limit far ABOVE what the upstream middleware reads, which is the shape any real ceiling will
    /// have: the pre-auth read is bounded to tens of kilobytes, and a ceiling is megabytes. It still
    /// answers 413, because the Content-Length check fires when the body is FIRST read rather than when
    /// the limit is passed — so how much of the body that middleware consumes does not change the answer,
    /// and tightening its read (or loosening the ceiling) cannot silently turn this into a 500.
    /// </summary>
    [Fact]
    public async Task A_limit_above_what_the_upstream_middleware_reads_is_still_413()
    {
        const long limit = 192 * 1024;
        await using var host = await KestrelPartnerHost.StartAsync(db.ConnectionString, limit);

        var response = await host.CreateClient().PostAsync(Route, JsonOf(limit + (64 * 1024)));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private static StringContent JsonOf(long sizeBytes)
    {
        var padding = new string('x', (int)sizeBytes);
        return new StringContent($$"""{"orderId":"{{padding}}","photos":[]}""", Encoding.UTF8, "application/json");
    }
}
