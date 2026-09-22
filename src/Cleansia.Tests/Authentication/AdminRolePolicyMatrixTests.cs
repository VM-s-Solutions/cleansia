using System.Reflection;
using System.Security.Claims;
using Cleansia.Config.Services;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// ADR-0066 D9.1 — every mapped permission × eight principals through the real
/// <see cref="IAuthorizationService"/> built by <c>AddCleansiaAuthorization</c>, in milliseconds, with no
/// host and no database. The expected answer for a permission is derived from the frozen D3 table
/// (<see cref="FrozenPermissionMapTests.ExpectedD3Map"/>) and a hand-written truth table of which
/// principal each physical policy admits; a <see cref="Policy"/> constant without a row in the frozen
/// table fails here by name, so a new policy cannot ship without a D3 expectation.
/// </summary>
public class AdminRolePolicyMatrixTests
{
    public enum Principal
    {
        Administrator,
        Manager,
        Support,
        Accountant,
        ClaimlessAdministrator,
        Employee,
        Customer,
        Anonymous,
    }

    private static readonly Principal[] Administrators =
    [
        Principal.Administrator, Principal.Manager, Principal.Support, Principal.Accountant, Principal.ClaimlessAdministrator,
    ];

    /// <summary>Who each physical policy admits when no resource is in play (OwnerOrElevated: the elevated arm only).</summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<Principal>> Admitted = new Dictionary<string, IReadOnlySet<Principal>>
    {
        [PhysicalPolicy.Authenticated] = Enum.GetValues<Principal>().Except([Principal.Anonymous]).ToHashSet(),
        [PhysicalPolicy.CustomerOnly] = new HashSet<Principal> { Principal.Customer },
        [PhysicalPolicy.EmployeeOrAdmin] = Administrators.Append(Principal.Employee).ToHashSet(),
        [PhysicalPolicy.AdminOnly] = Administrators.ToHashSet(),
        [PhysicalPolicy.OwnerOrElevated] = Administrators.ToHashSet(),
        [PhysicalPolicy.AdministratorOnly] = new HashSet<Principal> { Principal.Administrator },
        [PhysicalPolicy.ManagerOrAbove] = new HashSet<Principal> { Principal.Administrator, Principal.Manager },
        [PhysicalPolicy.SupportOrAbove] = new HashSet<Principal> { Principal.Administrator, Principal.Manager, Principal.Support },
        [PhysicalPolicy.AccountantOrAbove] = new HashSet<Principal> { Principal.Administrator, Principal.Manager, Principal.Accountant },
        [PhysicalPolicy.Deny] = new HashSet<Principal>(),
    };

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

    private static ClaimsPrincipal Build(Principal principal)
    {
        if (principal == Principal.Anonymous)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var (profile, role) = principal switch
        {
            Principal.Administrator => (UserProfile.Administrator, (AdminRole?)AdminRole.Administrator),
            Principal.Manager => (UserProfile.Administrator, AdminRole.Manager),
            Principal.Support => (UserProfile.Administrator, AdminRole.Support),
            Principal.Accountant => (UserProfile.Administrator, AdminRole.Accountant),
            Principal.ClaimlessAdministrator => (UserProfile.Administrator, null),
            Principal.Employee => (UserProfile.Employee, null),
            Principal.Customer => (UserProfile.Customer, null),
            _ => throw new ArgumentOutOfRangeException(nameof(principal)),
        };

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"sub-{principal}"),
            new(ClaimTypes.Role, profile.ToString()),
        };
        if (role is { } value)
        {
            claims.Add(new Claim(AdminRoleSets.ClaimType, value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private static IEnumerable<string> MappedPermissions() =>
        typeof(Policy)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(p => !PolicyBuilder.AnonymousAllowList.Contains(p))
            .OrderBy(p => p);

    [Fact]
    public void Every_Mapped_Permission_Has_A_Row_In_The_Frozen_Table_And_A_Known_Physical_Policy()
    {
        var withoutRow = MappedPermissions().Where(p => !FrozenPermissionMapTests.ExpectedD3Map.ContainsKey(p)).ToList();
        Assert.True(withoutRow.Count == 0, "Policy constants without a D3 expectation: " + string.Join(", ", withoutRow));

        var unknownPhysical = FrozenPermissionMapTests.ExpectedD3Map.Values.Distinct().Except(Admitted.Keys).ToList();
        Assert.True(unknownPhysical.Count == 0, "Physical policies without a truth-table row: " + string.Join(", ", unknownPhysical));
    }

    [Fact]
    public async Task Every_Mapped_Permission_Answers_Every_Principal_Per_D3()
    {
        await using var provider = BuildProvider();
        var authz = provider.GetRequiredService<IAuthorizationService>();
        var principals = Enum.GetValues<Principal>().ToDictionary(p => p, Build);
        var wrong = new List<string>();

        foreach (var permission in MappedPermissions())
        {
            var expectedPhysical = FrozenPermissionMapTests.ExpectedD3Map[permission];
            var physical = permission.ToPhysicalPolicy();
            if (physical != expectedPhysical)
            {
                wrong.Add($"{permission}: mapped to {physical}, D3 says {expectedPhysical}");
                continue;
            }

            foreach (var (principal, user) in principals)
            {
                var expected = Admitted[expectedPhysical].Contains(principal);
                var actual = (await authz.AuthorizeAsync(user, resource: null, physical)).Succeeded;
                if (actual != expected)
                {
                    wrong.Add($"{permission} ({physical}) × {principal}: expected {expected}, was {actual}");
                }
            }
        }

        Assert.True(wrong.Count == 0, "Matrix mismatches:\n" + string.Join("\n", wrong));
    }

    [Fact]
    public void The_Sets_Are_Registered_And_Every_Set_Name_Resolves()
    {
        string[] sets =
        [
            PhysicalPolicy.AdministratorOnly, PhysicalPolicy.ManagerOrAbove,
            PhysicalPolicy.SupportOrAbove, PhysicalPolicy.AccountantOrAbove,
        ];
        using var provider = BuildProvider();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (var set in sets)
        {
            Assert.NotNull(policies.GetPolicyAsync(set).GetAwaiter().GetResult());
            Assert.NotEmpty(AdminRoleSets.For(set));
        }
    }
}
