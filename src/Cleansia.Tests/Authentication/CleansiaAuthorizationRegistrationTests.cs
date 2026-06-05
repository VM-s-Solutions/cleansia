using System.Security.Claims;
using Cleansia.Config.Services;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// Verification #2 (ADR-0001 §D4) — the single shared registration is present on every
/// host AND semantically correct. Because all five hosts now call the identical
/// <c>AddCleansiaAuthorization</c>, testing that one registration is testing every host's behavior
/// (this is precisely the consolidation D4 buys). We assert:
///   (a) every PhysicalPolicy.* except Anonymous resolves to a non-null policy, and
///   (b) constructed admin / customer / employee principals get the right pass/fail.
/// </summary>
public class CleansiaAuthorizationRegistrationTests
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

    private static ClaimsPrincipal PrincipalWithRole(string? role)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-123") };
        if (role is not null)
            claims.Add(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private static readonly string[] RegisteredPhysicalPolicies =
    {
        PhysicalPolicy.Authenticated,
        PhysicalPolicy.CustomerOnly,
        PhysicalPolicy.EmployeeOrAdmin,
        PhysicalPolicy.AdminOnly,
        PhysicalPolicy.OwnerOrElevated,
        PhysicalPolicy.Deny,
    };

    [Fact]
    public async Task Every_NonAnonymous_PhysicalPolicy_Resolves_NonNull()
    {
        await using var provider = BuildProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (var name in RegisteredPhysicalPolicies)
        {
            var policy = await policyProvider.GetPolicyAsync(name);
            Assert.True(policy is not null, $"Physical policy '{name}' is not registered.");
        }

        // Anonymous is NOT a registered policy (it is [AllowAnonymous]).
        Assert.Null(await policyProvider.GetPolicyAsync(PhysicalPolicy.Anonymous));
    }

    [Fact]
    public async Task Admin_Passes_AdminOnly_And_Fails_CustomerOnly()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var admin = PrincipalWithRole(UserProfile.Administrator.ToString());

        Assert.True((await authz.AuthorizeAsync(admin, resource: null, PhysicalPolicy.AdminOnly)).Succeeded);
        Assert.False((await authz.AuthorizeAsync(admin, resource: null, PhysicalPolicy.CustomerOnly)).Succeeded);
        Assert.True((await authz.AuthorizeAsync(admin, resource: null, PhysicalPolicy.EmployeeOrAdmin)).Succeeded);
    }

    [Fact]
    public async Task Customer_Fails_AdminOnly_And_Passes_CustomerOnly()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var customer = PrincipalWithRole(UserProfile.Customer.ToString());

        Assert.False((await authz.AuthorizeAsync(customer, resource: null, PhysicalPolicy.AdminOnly)).Succeeded);
        Assert.True((await authz.AuthorizeAsync(customer, resource: null, PhysicalPolicy.CustomerOnly)).Succeeded);
        Assert.False((await authz.AuthorizeAsync(customer, resource: null, PhysicalPolicy.EmployeeOrAdmin)).Succeeded);
    }

    [Fact]
    public async Task Employee_Passes_EmployeeOrAdmin_And_Fails_AdminOnly_And_CustomerOnly()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var employee = PrincipalWithRole(UserProfile.Employee.ToString());

        Assert.True((await authz.AuthorizeAsync(employee, resource: null, PhysicalPolicy.EmployeeOrAdmin)).Succeeded);
        Assert.False((await authz.AuthorizeAsync(employee, resource: null, PhysicalPolicy.AdminOnly)).Succeeded);
        Assert.False((await authz.AuthorizeAsync(employee, resource: null, PhysicalPolicy.CustomerOnly)).Succeeded);
    }

    [Fact]
    public async Task Deny_Sentinel_Refuses_Everyone()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();

        var admin = PrincipalWithRole(UserProfile.Administrator.ToString());
        var customer = PrincipalWithRole(UserProfile.Customer.ToString());

        Assert.False((await authz.AuthorizeAsync(admin, resource: null, PhysicalPolicy.Deny)).Succeeded);
        Assert.False((await authz.AuthorizeAsync(customer, resource: null, PhysicalPolicy.Deny)).Succeeded);
    }

    [Fact]
    public async Task CustomerOnly_Resolves_On_Every_Host_Registration()
    {
        // CustomerOnly is now registered everywhere (it used to be missing on
        // Admin/Partner/Mobile.Partner). Routed onto any host it resolves and denies an admin.
        await using var provider = BuildProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        Assert.NotNull(await policyProvider.GetPolicyAsync(PhysicalPolicy.CustomerOnly));
    }
}
