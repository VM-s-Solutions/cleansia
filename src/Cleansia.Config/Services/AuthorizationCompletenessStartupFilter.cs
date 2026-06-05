using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.Services;

/// <summary>
/// Startup filter (ADR-0001 §D1.2 / §D4.2) that fails the host boot — in dev and CI, before the
/// first request — if the authorization seam is incomplete or misbehaving.
///
/// <para><see cref="PolicyBuilder.AssertComplete"/> runs at filter CONSTRUCTION (it is a pure static
/// reflection check needing no DI).</para>
/// <para>The presence + behavior assertions run in <see cref="Configure"/>, where
/// <see cref="IApplicationBuilder.ApplicationServices"/> exposes the built provider that resolves
/// <see cref="IAuthorizationPolicyProvider"/> and <see cref="IAuthorizationService"/>.</para>
/// </summary>
public sealed class AuthorizationCompletenessStartupFilter : IStartupFilter
{
    public AuthorizationCompletenessStartupFilter()
    {
        // Completeness: every Policy.* is mapped or allow-listed; no orphans; no contradictions.
        PolicyBuilder.AssertComplete();
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            AssertAllRegistered(app.ApplicationServices);
            AssertSemantics(app.ApplicationServices);
            next(app);
        };

    /// <summary>Presence: every PhysicalPolicy.* except Anonymous resolves to a non-null policy.</summary>
    private static void AssertAllRegistered(IServiceProvider provider)
    {
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        string[] required =
        {
            PhysicalPolicy.Authenticated,
            PhysicalPolicy.CustomerOnly,
            PhysicalPolicy.EmployeeOrAdmin,
            PhysicalPolicy.AdminOnly,
            PhysicalPolicy.OwnerOrElevated,
            PhysicalPolicy.Deny,
        };

        var unregistered = required
            .Where(name => policyProvider.GetPolicyAsync(name).GetAwaiter().GetResult() is null)
            .ToList();

        if (unregistered.Count > 0)
            throw new InvalidOperationException(
                "Authorization registration incomplete on this host. Unregistered physical " +
                "policies: " + string.Join(", ", unregistered));
    }

    /// <summary>
    /// Behavior parity (not just name parity): a constructed admin passes AdminOnly and fails
    /// CustomerOnly; a constructed customer fails AdminOnly and passes CustomerOnly; an employee
    /// passes EmployeeOrAdmin. Catches a wrong duplicate winning or UserRole/UserProfile drift.
    /// </summary>
    private static void AssertSemantics(IServiceProvider provider)
    {
        var authz = provider.GetRequiredService<IAuthorizationService>();

        var admin = PrincipalWithRole(UserProfile.Administrator.ToString());
        var customer = PrincipalWithRole(UserProfile.Customer.ToString());
        var employee = PrincipalWithRole(UserProfile.Employee.ToString());

        Require(authz, admin, PhysicalPolicy.AdminOnly, expected: true);
        Require(authz, admin, PhysicalPolicy.CustomerOnly, expected: false);
        Require(authz, customer, PhysicalPolicy.AdminOnly, expected: false);
        Require(authz, customer, PhysicalPolicy.CustomerOnly, expected: true);
        Require(authz, employee, PhysicalPolicy.EmployeeOrAdmin, expected: true);
        Require(authz, admin, PhysicalPolicy.Deny, expected: false);
        Require(authz, customer, PhysicalPolicy.Deny, expected: false);
    }

    private static void Require(
        IAuthorizationService authz, ClaimsPrincipal principal, string policy, bool expected)
    {
        var actual = authz.AuthorizeAsync(principal, resource: null, policy)
            .GetAwaiter().GetResult().Succeeded;
        if (actual != expected)
            throw new InvalidOperationException(
                $"Authorization policy '{policy}' misbehaves: expected Succeeded={expected} for a " +
                $"principal with the relevant role, but was {actual}. Check UserRole/UserProfile parity.");
    }

    private static ClaimsPrincipal PrincipalWithRole(string role) =>
        new(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "startup-assertion"),
                new Claim(ClaimTypes.Role, role),
            },
            authenticationType: "StartupAssertion"));
}
