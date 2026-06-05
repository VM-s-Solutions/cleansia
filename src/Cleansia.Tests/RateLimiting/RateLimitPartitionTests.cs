using System.Security.Claims;
using Cleansia.Config.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace Cleansia.Tests.RateLimiting;

/// <summary>
/// ADR-0003 D2 — the partition-key functions, unit-tested in isolation (pure logic; no host needed).
/// These prove the <em>key selection</em> that the host-level isolation tests (AC1/AC2) then prove
/// actually isolates callers through the real middleware.
/// </summary>
public class RateLimitPartitionTests
{
    private static HttpContext Anonymous(string? ip)
    {
        var ctx = new DefaultHttpContext();
        if (ip is not null) ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity()); // not authenticated
        return ctx;
    }

    private static HttpContext Authenticated(string sub, string ip)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, sub) }, authenticationType: "Test"));
        return ctx;
    }

    // D2 — anonymous auth → per real client IP, namespaced "auth:ip:".
    [Fact]
    public void AuthPartition_Anonymous_Keys_By_Ip()
    {
        var (key, anon) = RateLimitPolicies.AuthPartitionKey(Anonymous("10.0.0.1"));
        Assert.True(anon);
        Assert.Equal("auth:ip:10.0.0.1", key);
    }

    // D2 — distinct anonymous IPs → distinct partitions (the core of AC1).
    [Fact]
    public void AuthPartition_Distinct_Ips_Distinct_Keys()
    {
        var a = RateLimitPolicies.AuthPartitionKey(Anonymous("10.0.0.1")).key;
        var b = RateLimitPolicies.AuthPartitionKey(Anonymous("10.0.0.2")).key;
        Assert.NotEqual(a, b);
    }

    // D2 — authenticated auth → per sub, namespaced "auth:sub:", flagged non-anonymous (the core of AC2).
    [Fact]
    public void AuthPartition_Authenticated_Keys_By_Sub_Not_Ip()
    {
        var (keyX, anonX) = RateLimitPolicies.AuthPartitionKey(Authenticated("user-X", "10.0.0.9"));
        var (keyY, _)     = RateLimitPolicies.AuthPartitionKey(Authenticated("user-Y", "10.0.0.9")); // SAME ip
        Assert.False(anonX);
        Assert.Equal("auth:sub:user-X", keyX);
        Assert.NotEqual(keyX, keyY); // same IP, different sub → different partition
    }

    // D2 — namespaces never alias: a sub equal to an IP string can never share a window.
    [Fact]
    public void Auth_And_Interactive_Namespaces_Are_Disjoint()
    {
        var authKey = RateLimitPolicies.AuthPartitionKey(Authenticated("10.0.0.1", "10.0.0.1")).key;
        var interKey = RateLimitPolicies.InteractivePartitionKey(Authenticated("10.0.0.1", "10.0.0.1")).key;
        Assert.StartsWith("auth:", authKey);
        Assert.StartsWith("interactive:", interKey);
        Assert.NotEqual(authKey, interKey);
    }

    // D2 — interactive policy uses the same branch logic.
    [Fact]
    public void InteractivePartition_Anonymous_Keys_By_Ip_Authenticated_By_Sub()
    {
        Assert.Equal("interactive:ip:10.0.0.5",
            RateLimitPolicies.InteractivePartitionKey(Anonymous("10.0.0.5")).key);
        Assert.Equal("interactive:sub:user-Z",
            RateLimitPolicies.InteractivePartitionKey(Authenticated("user-Z", "10.0.0.5")).key);
    }

    // D2 — missing IP collapses to a single deny-leaning "unknown" partition (not per-request).
    [Fact]
    public void ClientIp_Missing_Is_Single_Unknown_Bucket()
    {
        Assert.Equal("unknown", RateLimitPolicies.ClientIp(Anonymous(null)));
        Assert.Equal("auth:ip:unknown", RateLimitPolicies.AuthPartitionKey(Anonymous(null)).key);
    }

    // D2 — authenticated-without-sub (anomalous) falls back to the IP partition (deny-leaning).
    [Fact]
    public void AuthPartition_Authenticated_Without_Sub_Falls_Back_To_Ip()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.7");
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test")); // authed, no NameIdentifier
        var (key, anon) = RateLimitPolicies.AuthPartitionKey(ctx);
        Assert.True(anon);
        Assert.Equal("auth:ip:10.0.0.7", key);
    }
}
