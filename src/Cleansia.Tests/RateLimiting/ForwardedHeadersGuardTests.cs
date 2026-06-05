using System.Net;
using Cleansia.Config.RateLimiting;
using Microsoft.AspNetCore.Builder;

namespace Cleansia.Tests.RateLimiting;

/// <summary>
/// ADR-0003 D3 / AC5 — the FAIL-CLOSED startup guard, unit-tested as the pure predicate it is.
/// Non-Development MUST refuse to boot when forwarded-headers trust is unset or over-broad; the
/// host-level boot test (ForwardedHeadersBootTests) then proves the same predicate actually aborts
/// host start via the options Validate() pipeline.
/// </summary>
public class ForwardedHeadersGuardTests
{
    private static ForwardedHeadersOptions WithNetworks(params string[] cidrs)
    {
        var o = new ForwardedHeadersOptions();
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
        foreach (var c in cidrs) o.KnownIPNetworks.Add(IPNetwork.Parse(c));
        return o;
    }

    // --- Non-Development: must FAIL closed ---

    [Fact]
    public void NonDev_Empty_Networks_And_Proxies_Fails()
    {
        var o = WithNetworks(); // both empty
        Assert.False(RateLimitPolicies.ValidateForwardedHeadersConfig(o, isDevelopment: false));
    }

    [Theory]
    [InlineData("0.0.0.0/0")]   // explicit IPv4 all
    [InlineData("::/0")]        // explicit IPv6 all
    [InlineData("10.0.0.0/0")]  // /0 supernet
    [InlineData("10.0.0.0/8")]  // /8 supernet — boundary of the forbidden /0–/8 range
    [InlineData("0.0.0.0/1")]
    public void NonDev_OverBroad_Network_Fails(string cidr)
    {
        var o = WithNetworks(cidr);
        Assert.False(RateLimitPolicies.ValidateForwardedHeadersConfig(o, isDevelopment: false));
    }

    [Fact]
    public void NonDev_OverBroad_Among_Narrow_Still_Fails()
    {
        var o = WithNetworks("10.0.0.0/24", "172.16.0.0/8"); // one narrow, one /8
        Assert.False(RateLimitPolicies.ValidateForwardedHeadersConfig(o, isDevelopment: false));
    }

    // --- Non-Development: must PASS on a narrow, set config ---

    [Theory]
    [InlineData("10.0.0.0/9")]   // just past the /8 boundary
    [InlineData("10.0.0.0/24")]
    [InlineData("169.254.1.0/32")]
    public void NonDev_Narrow_Network_Passes(string cidr)
    {
        var o = WithNetworks(cidr);
        Assert.True(RateLimitPolicies.ValidateForwardedHeadersConfig(o, isDevelopment: false));
    }

    [Fact]
    public void NonDev_Only_KnownProxies_Set_Passes()
    {
        var o = new ForwardedHeadersOptions();
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
        o.KnownProxies.Add(IPAddress.Parse("10.0.0.4"));
        Assert.True(RateLimitPolicies.ValidateForwardedHeadersConfig(o, isDevelopment: false));
    }

    // --- Development: an empty/over-broad config is allowed (degrades to one bucket) ---

    [Fact]
    public void Dev_Empty_Config_Passes_Degrades_To_One_Bucket()
    {
        var o = WithNetworks();
        Assert.True(RateLimitPolicies.ValidateForwardedHeadersConfig(o, isDevelopment: true));
    }

    [Fact]
    public void Dev_OverBroad_Config_Passes()
    {
        var o = WithNetworks("0.0.0.0/0");
        Assert.True(RateLimitPolicies.ValidateForwardedHeadersConfig(o, isDevelopment: true));
    }
}
