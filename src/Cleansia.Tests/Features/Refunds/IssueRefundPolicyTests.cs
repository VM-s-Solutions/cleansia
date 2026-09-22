using System.Security.Claims;
using Cleansia.Config.Services;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Tests.Features.Refunds;

/// <summary>
/// TC-REFUND-ADMINONLY (ADR-0001 §D2, re-mapped by ADR-0066 D3) — the admin partial-refund permission
/// resolves to SupportOrAbove ("refunds within the policy" are Support's): a non-admin (Customer or
/// Employee) is denied; a Support passes; an Accountant does not. Mirrors
/// <c>DisputeMessagePolicyTests</c> — the buildable tier-1 outer-gate coverage; HTTP 403/200 rides a
/// separate harness.
/// </summary>
public class IssueRefundPolicyTests
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
    public void CanIssueRefund_ResolvesToSupportOrAbove()
    {
        Assert.Equal(PhysicalPolicy.SupportOrAbove, Policy.CanIssueRefund.ToPhysicalPolicy());
    }

    [Theory]
    [InlineData(UserProfile.Customer)]
    [InlineData(UserProfile.Employee)]
    public async Task NonAdmin_Is_Denied_The_IssueRefund_Permission(UserProfile role)
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var physical = Policy.CanIssueRefund.ToPhysicalPolicy();

        var result = await authz.AuthorizeAsync(Principal(role), new DefaultHttpContext(), physical);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(AdminRole.Administrator)]
    [InlineData(AdminRole.Support)]
    public async Task Admin_Is_Allowed_The_IssueRefund_Permission(AdminRole adminRole)
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var physical = Policy.CanIssueRefund.ToPhysicalPolicy();

        var result = await authz.AuthorizeAsync(
            Principal(UserProfile.Administrator, adminRole), new DefaultHttpContext(), physical);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task An_Accountant_Is_Denied_The_IssueRefund_Permission()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var physical = Policy.CanIssueRefund.ToPhysicalPolicy();

        var result = await authz.AuthorizeAsync(
            Principal(UserProfile.Administrator, AdminRole.Accountant), new DefaultHttpContext(), physical);

        Assert.False(result.Succeeded);
    }
}
