using Cleansia.Tests.RateLimiting.Harness;
using Microsoft.Extensions.Hosting;

namespace Cleansia.Tests.RateLimiting;

/// <summary>
/// ADR-0003 D3 / AC5 (verify #4) — the fail-closed guard observed END-TO-END: a non-Development
/// host must REFUSE TO BOOT (the options Validate() throws during StartAsync) when forwarded-headers
/// trust is unset or over-broad; Development boots and degrades.
/// </summary>
[Collection("RateLimiterHost")]
public class ForwardedHeadersBootTests
{
    private static Dictionary<string, string?> WithForwarded(string? proxies, string? networks)
    {
        var cfg = RateLimitTestConfig.Valid();
        cfg["ForwardedHeaders:KnownProxies"] = proxies;
        cfg["ForwardedHeaders:KnownNetworks"] = networks;
        return cfg;
    }

    [Fact]
    public async Task NonDev_Empty_Config_Refuses_To_Boot()
    {
        var ex = await RateLimiterHostHarness.TryBootThrows(
            WithForwarded(proxies: "", networks: ""), Environments.Production);
        Assert.NotNull(ex);
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    [InlineData("10.0.0.0/8")]
    public async Task NonDev_OverBroad_Networks_Refuses_To_Boot(string cidr)
    {
        var ex = await RateLimiterHostHarness.TryBootThrows(
            WithForwarded(proxies: "", networks: cidr), Environments.Production);
        Assert.NotNull(ex);
    }

    [Fact]
    public async Task NonDev_Narrow_Networks_Boots()
    {
        var ex = await RateLimiterHostHarness.TryBootThrows(
            WithForwarded(proxies: "", networks: "10.0.0.0/24"), Environments.Production);
        Assert.Null(ex);
    }

    [Fact]
    public async Task NonDev_KnownProxies_Set_Boots()
    {
        var ex = await RateLimiterHostHarness.TryBootThrows(
            WithForwarded(proxies: "127.0.0.1", networks: ""), Environments.Production);
        Assert.Null(ex);
    }

    [Fact]
    public async Task Dev_Empty_Config_Boots_Degraded()
    {
        var ex = await RateLimiterHostHarness.TryBootThrows(
            WithForwarded(proxies: "", networks: ""), Environments.Development);
        Assert.Null(ex);
    }
}
