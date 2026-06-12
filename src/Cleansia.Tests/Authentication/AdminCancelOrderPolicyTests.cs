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
/// AC7 (ADR-0001 D1/D2) — the new admin-cancel permission is <c>AdminOnly</c>, fail-closed, and mapped.
/// An Administrator passes; a Customer or Employee is denied (the privileged money+lifecycle write
/// never leaks to a non-admin role).
/// </summary>
public class AdminCancelOrderPolicyTests
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

    [Fact]
    public void CanAdminCancelOrder_Is_Mapped_AdminOnly()
    {
        Assert.Equal(PhysicalPolicy.AdminOnly, Policy.CanAdminCancelOrder.ToPhysicalPolicy());
    }

    [Fact]
    public async Task Admin_Is_Allowed()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var physical = Policy.CanAdminCancelOrder.ToPhysicalPolicy();

        var result = await authz.AuthorizeAsync(
            Principal(UserProfile.Administrator), new DefaultHttpContext(), physical);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(UserProfile.Customer)]
    [InlineData(UserProfile.Employee)]
    public async Task NonAdmin_Is_Denied(UserProfile role)
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var physical = Policy.CanAdminCancelOrder.ToPhysicalPolicy();

        var result = await authz.AuthorizeAsync(Principal(role), new DefaultHttpContext(), physical);

        Assert.False(result.Succeeded);
    }
}
