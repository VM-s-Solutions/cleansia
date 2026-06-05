using System.Security.Claims;
using Cleansia.Config.Services;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// T-0102 (SEC-DSP-01) / ADR-0001 §D2 Note C, verification #5 — the dispute-message permission split
/// at the policy (outer-gate) layer, resolved through the shared <c>AddCleansiaAuthorization</c>:
///   - AC2: the staff-reply <see cref="Policy.CanRespondToDispute"/> resolves to AdminOnly — a
///     non-Admin (Customer or Employee) is denied; an Administrator passes.
///   - AC1: the customer self-reply <see cref="Policy.CanAddDisputeMessage"/> resolves to CustomerOnly —
///     a customer passes; an Employee/Admin is denied (the staff path is a separate permission).
/// This is the buildable tier-1 coverage (ADR-0001 §D6); the HTTP 403/200 cases ride the T-0126 harness.
/// </summary>
public class DisputeMessagePolicyTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "test-secret-value-that-is-long-enough-for-hs256-xx",
                ["JwtSettings:Issuer"] = "cleansia",
            })
            .Build();

        services.AddCleansiaAuthorization(configuration);
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal Principal(UserProfile role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "sub-1"),
            new(ClaimTypes.Role, role.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    // ── AC2 — staff reply is AdminOnly ─────────────────────────────────────

    [Theory]
    [InlineData(UserProfile.Customer)]
    [InlineData(UserProfile.Employee)]
    public async Task NonAdmin_Is_Denied_The_Staff_Reply_Permission(UserProfile role)
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var physical = Policy.CanRespondToDispute.ToPhysicalPolicy();

        var result = await authz.AuthorizeAsync(Principal(role), new DefaultHttpContext(), physical);

        Assert.Equal(PhysicalPolicy.AdminOnly, physical);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Admin_Is_Allowed_The_Staff_Reply_Permission()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var physical = Policy.CanRespondToDispute.ToPhysicalPolicy();

        var result = await authz.AuthorizeAsync(
            Principal(UserProfile.Administrator), new DefaultHttpContext(), physical);

        Assert.True(result.Succeeded);
    }

    // ── AC1 — customer self-reply is CustomerOnly ──────────────────────────

    [Fact]
    public async Task Customer_Is_Allowed_The_AddDisputeMessage_Permission()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var physical = Policy.CanAddDisputeMessage.ToPhysicalPolicy();

        var result = await authz.AuthorizeAsync(
            Principal(UserProfile.Customer), new DefaultHttpContext(), physical);

        Assert.Equal(PhysicalPolicy.CustomerOnly, physical);
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(UserProfile.Employee)]
    [InlineData(UserProfile.Administrator)]
    public async Task NonCustomer_Is_Denied_The_AddDisputeMessage_Permission(UserProfile role)
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var physical = Policy.CanAddDisputeMessage.ToPhysicalPolicy();

        var result = await authz.AuthorizeAsync(Principal(role), new DefaultHttpContext(), physical);

        Assert.False(result.Succeeded);
    }
}
