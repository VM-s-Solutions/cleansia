using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cleansia.HostTests.Infrastructure;

/// <summary>
/// The real Partner host on a real <c>Kestrel</c> bound to a loopback port, configured with a request
/// body limit the test chooses.
///
/// <para><see cref="HostTestApplicationFactory{TEntryPoint}"/> cannot answer a question about a body
/// limit: <c>TestServer</c> is an in-memory server, so both halves of the behaviour under test belong to
/// a component it replaces — the enforcement of <c>MaxRequestBodySize</c>, and the translation of the
/// resulting <c>BadHttpRequestException</c> into a status code on the wire. A TestServer answer would be
/// an answer about TestServer.</para>
///
/// <para>Everything else is deliberately identical to that factory — same entry point, same
/// "HostTests" environment (so the boot guards see the same config and Swagger stays fail-closed off),
/// same last-layered settings file, same Testcontainers connection string — so the pipeline under the
/// request is the production one.</para>
/// </summary>
public sealed class KestrelPartnerHost : IAsyncDisposable
{
    private readonly IHost _host;

    private KestrelPartnerHost(IHost host) => _host = host;

    public static async Task<KestrelPartnerHost> StartAsync(string connectionString, long maxRequestBodySizeBytes)
    {
        var builder = Cleansia.Web.Partner.Program.CreateHostBuilder([]);

        builder.ConfigureWebHost(web =>
        {
            web.UseContentRoot(ContentRootOf("Cleansia.Web.Partner"));
            web.UseEnvironment("HostTests");
            web.ConfigureAppConfiguration((_, config) =>
            {
                config.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.HostTests.json"), optional: false);
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ConnectionString"] = connectionString,
                });
            });
            web.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxRequestBodySizeBytes);
            web.UseUrls("http://127.0.0.1:0");
        });

        var host = builder.Build();
        await host.StartAsync();
        return new KestrelPartnerHost(host);
    }

    public HttpClient CreateClient() => new()
    {
        BaseAddress = new Uri(_host.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First()),
    };

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    /// <summary>
    /// The same manifest <c>WebApplicationFactory</c> itself reads to point a test at another project's
    /// content root, so the host finds its own appsettings.json exactly as it does under that factory.
    /// </summary>
    private static string ContentRootOf(string assemblyName)
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "MvcTestingAppManifest.json");
        var manifest = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(manifestPath))!;

        return manifest.First(entry => entry.Key.StartsWith($"{assemblyName},", StringComparison.Ordinal)).Value;
    }
}
