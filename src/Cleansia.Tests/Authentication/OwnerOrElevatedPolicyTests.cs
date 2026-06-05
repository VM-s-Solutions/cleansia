using System.Security.Claims;
using Cleansia.Config.Services;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// ADR-0001 §D3 — the redefined <see cref="PhysicalPolicy.OwnerOrElevated"/>
/// resolver inside the shared <c>AddCleansiaAuthorization</c>. Proves the IDOR hole is closed at the
/// policy (outer-gate) layer:
///   - elevated == Admin ONLY (the blanket Employee→true over-grant is gone).
///   - a non-admin owner (sub == requested id) passes, via route "id", route "userId", OR
///     query "UserId" (the canonical resolver replacing the RouteValues["id"]-only read); a
///     non-owner non-admin fails.
///   - a non-HttpContext resource fails closed (deny), never an over-grant.
/// Each test predates the resolver fix (red → green) per knowledge/testing.md.
/// </summary>
public class OwnerOrElevatedPolicyTests
{
    private const string OwnerId = "owner-sub-123";
    private const string OtherId = "other-sub-456";

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

    private static ClaimsPrincipal Principal(string sub, string? role)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, sub) };
        if (role is not null)
            claims.Add(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private static HttpContext HttpContextWithRouteId(string key, string value)
    {
        var http = new DefaultHttpContext();
        http.Request.RouteValues[key] = value;
        return http;
    }

    private static HttpContext HttpContextWithQueryUserId(string value)
    {
        var http = new DefaultHttpContext();
        http.Request.Query = new QueryCollection(
            new Dictionary<string, StringValues> { ["UserId"] = value });
        return http;
    }

    [Fact]
    public async Task Employee_Requesting_NotOwn_Id_Fails()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var employee = Principal(OwnerId, UserProfile.Employee.ToString());
        var http = HttpContextWithQueryUserId(OtherId);

        var result = await authz.AuthorizeAsync(employee, http, PhysicalPolicy.OwnerOrElevated);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Admin_Requesting_Any_Id_Passes()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var admin = Principal("admin-sub-999", UserProfile.Administrator.ToString());
        var http = HttpContextWithQueryUserId(OtherId);

        var result = await authz.AuthorizeAsync(admin, http, PhysicalPolicy.OwnerOrElevated);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task NonAdmin_Owner_Passes_When_Id_In_Route_Id()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var owner = Principal(OwnerId, UserProfile.Employee.ToString());
        var http = HttpContextWithRouteId("id", OwnerId);

        var result = await authz.AuthorizeAsync(owner, http, PhysicalPolicy.OwnerOrElevated);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task NonAdmin_Owner_Passes_When_Id_In_Route_UserId()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var owner = Principal(OwnerId, UserProfile.Employee.ToString());
        var http = HttpContextWithRouteId("userId", OwnerId);

        var result = await authz.AuthorizeAsync(owner, http, PhysicalPolicy.OwnerOrElevated);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task NonAdmin_Owner_Passes_When_Id_In_Query_UserId()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var owner = Principal(OwnerId, UserProfile.Customer.ToString());
        var http = HttpContextWithQueryUserId(OwnerId);

        var result = await authz.AuthorizeAsync(owner, http, PhysicalPolicy.OwnerOrElevated);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task NonAdmin_Fails_When_Subject_Id_Does_Not_Match_Sub()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var nonOwner = Principal(OwnerId, UserProfile.Customer.ToString());
        var http = HttpContextWithQueryUserId(OtherId);

        var result = await authz.AuthorizeAsync(nonOwner, http, PhysicalPolicy.OwnerOrElevated);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task NonAdmin_Fails_When_Resource_Is_Not_HttpContext()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var owner = Principal(OwnerId, UserProfile.Employee.ToString());

        // resource: null (and any non-HttpContext) must deny for a non-admin — never an over-grant.
        var result = await authz.AuthorizeAsync(owner, resource: null, PhysicalPolicy.OwnerOrElevated);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Admin_Still_Passes_Even_When_Resource_Is_Not_HttpContext()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var admin = Principal("admin-sub-999", UserProfile.Administrator.ToString());

        var result = await authz.AuthorizeAsync(admin, resource: null, PhysicalPolicy.OwnerOrElevated);

        Assert.True(result.Succeeded);
    }
}
