using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// The lattice, spelled once: Administrator ⊇ Manager ⊇ (Support ∪ Accountant), "any" is the four,
/// a set name resolves to its set and nothing else resolves at all, and the principal check fails
/// closed on every shape that is not an administrator carrying a real role inside the set.
/// </summary>
public class AdminRoleSetsTests
{
    [Fact]
    public void The_Sets_Are_The_Lattice()
    {
        Assert.Equal([AdminRole.Administrator], AdminRoleSets.AdministratorOnly.Order());
        Assert.Equal([AdminRole.Administrator, AdminRole.Manager], AdminRoleSets.ManagerOrAbove.Order());
        Assert.Equal([AdminRole.Administrator, AdminRole.Manager, AdminRole.Support], AdminRoleSets.SupportOrAbove.Order());
        Assert.Equal([AdminRole.Administrator, AdminRole.Manager, AdminRole.Accountant], AdminRoleSets.AccountantOrAbove.Order());
        Assert.Equal(Enum.GetValues<AdminRole>().Order(), AdminRoleSets.Any.Order());
    }

    [Theory]
    [InlineData(PhysicalPolicy.AdminOnly, 4)]
    [InlineData(PhysicalPolicy.AdministratorOnly, 1)]
    [InlineData(PhysicalPolicy.ManagerOrAbove, 2)]
    [InlineData(PhysicalPolicy.SupportOrAbove, 3)]
    [InlineData(PhysicalPolicy.AccountantOrAbove, 3)]
    public void For_Resolves_Each_Set_Name(string physicalPolicy, int size)
    {
        Assert.Equal(size, AdminRoleSets.For(physicalPolicy).Count);
    }

    [Theory]
    [InlineData(PhysicalPolicy.Authenticated)]
    [InlineData(PhysicalPolicy.EmployeeOrAdmin)]
    [InlineData(PhysicalPolicy.CustomerOnly)]
    [InlineData(PhysicalPolicy.OwnerOrElevated)]
    [InlineData(PhysicalPolicy.Deny)]
    [InlineData("Support")]
    public void For_Refuses_A_Name_That_Is_Not_An_Administrator_Set(string physicalPolicy)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AdminRoleSets.For(physicalPolicy));
    }

    [Theory]
    [InlineData(AdminRole.Administrator, true)]
    [InlineData(AdminRole.Manager, true)]
    [InlineData(AdminRole.Support, true)]
    [InlineData(AdminRole.Accountant, false)]
    public void Admits_An_Administrator_Whose_Role_Is_In_The_Set(AdminRole role, bool admitted)
    {
        Assert.Equal(admitted, AdminRoleSets.Admits(Principal(UserProfile.Administrator, role.ToString()), AdminRoleSets.SupportOrAbove));
    }

    [Fact]
    public void Refuses_An_Administrator_With_No_Role_Claim_On_Every_Set()
    {
        var claimless = Principal(UserProfile.Administrator, roleClaim: null);

        Assert.False(AdminRoleSets.Admits(claimless, AdminRoleSets.AdministratorOnly));
        Assert.False(AdminRoleSets.Admits(claimless, AdminRoleSets.ManagerOrAbove));
        Assert.False(AdminRoleSets.Admits(claimless, AdminRoleSets.SupportOrAbove));
        Assert.False(AdminRoleSets.Admits(claimless, AdminRoleSets.AccountantOrAbove));
        Assert.False(AdminRoleSets.Admits(claimless, AdminRoleSets.Any));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Owner")]
    [InlineData("99")]
    [InlineData("1; DROP")]
    public void Refuses_A_Claim_That_Is_Not_A_Role(string roleClaim)
    {
        Assert.False(AdminRoleSets.Admits(Principal(UserProfile.Administrator, roleClaim), AdminRoleSets.Any));
    }

    [Theory]
    [InlineData(UserProfile.Employee)]
    [InlineData(UserProfile.Customer)]
    public void Refuses_A_Non_Administrator_Even_With_A_Role_Claim(UserProfile profile)
    {
        Assert.False(AdminRoleSets.Admits(Principal(profile, AdminRole.Administrator.ToString()), AdminRoleSets.Any));
    }

    [Fact]
    public void Refuses_An_Anonymous_Principal()
    {
        Assert.False(AdminRoleSets.Admits(new ClaimsPrincipal(new ClaimsIdentity()), AdminRoleSets.Any));
    }

    private static ClaimsPrincipal Principal(UserProfile profile, string? roleClaim)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "sub-1"),
            new(ClaimTypes.Role, profile.ToString()),
        };
        if (roleClaim is not null)
        {
            claims.Add(new Claim(AdminRoleSets.ClaimType, roleClaim));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }
}
