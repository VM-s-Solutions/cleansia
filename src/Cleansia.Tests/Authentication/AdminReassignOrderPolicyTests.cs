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
/// ADR-0001 D1/D2, re-mapped by ADR-0066 D3 — the reassign permission is <c>SupportOrAbove</c>, fail-closed, and mapped. An
/// Administrator passes; a Customer or Employee is denied — the privileged assignment write never
/// leaks to a non-admin role.
/// </summary>
public class AdminReassignOrderPolicyTests
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

    private static ClaimsPrincipal Principal(UserProfile role, AdminRole? adminRole = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "sub-1"),
            new(ClaimTypes.Role, role.ToString()),
        };
        if (adminRole is { } value)
        {
            claims.Add(new Claim(AdminRoleSets.ClaimType, value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    [Fact]
    public void CanReassignOrder_Is_Mapped_SupportOrAbove()
    {
        Assert.Equal(PhysicalPolicy.SupportOrAbove, Policy.CanReassignOrder.ToPhysicalPolicy());
    }

    [Fact]
    public async Task Admin_Is_Allowed()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var physical = Policy.CanReassignOrder.ToPhysicalPolicy();

        var result = await authz.AuthorizeAsync(
            Principal(UserProfile.Administrator, AdminRole.Administrator), new DefaultHttpContext(), physical);

        Assert.True(result.Succeeded);
    }

    // An order-ops act is Support's area: the Accountant and a claimless Administrator token are refused.
    [Theory]
    [InlineData(AdminRole.Accountant)]
    [InlineData(null)]
    public async Task Administrator_Outside_The_Support_Set_Is_Denied(AdminRole? adminRole)
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var physical = Policy.CanReassignOrder.ToPhysicalPolicy();

        var result = await authz.AuthorizeAsync(
            Principal(UserProfile.Administrator, adminRole), new DefaultHttpContext(), physical);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(UserProfile.Customer)]
    [InlineData(UserProfile.Employee)]
    public async Task NonAdmin_Is_Denied(UserProfile role)
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var physical = Policy.CanReassignOrder.ToPhysicalPolicy();

        var result = await authz.AuthorizeAsync(Principal(role), new DefaultHttpContext(), physical);

        Assert.False(result.Succeeded);
    }
}
