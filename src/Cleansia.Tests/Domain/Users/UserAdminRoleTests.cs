using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Domain.Users;

/// <summary>
/// The administrator's role is a second axis on the account, meaningful only for the Administrator
/// profile: the factory refuses an administrator without a role and a role on any other profile — the
/// invariant the database check constraint enforces — and the only later writer refuses a non-administrator.
/// </summary>
public class UserAdminRoleTests
{
    private static User Create(UserProfile profile, AdminRole? role) =>
        User.CreateWithPassword("who@example.com", "Password1!", "First", "Last", profile, adminRole: role);

    [Theory]
    [InlineData(AdminRole.Administrator)]
    [InlineData(AdminRole.Manager)]
    [InlineData(AdminRole.Support)]
    [InlineData(AdminRole.Accountant)]
    public void An_Administrator_Is_Created_With_Its_Role(AdminRole role)
    {
        var user = Create(UserProfile.Administrator, role);

        Assert.Equal(UserProfile.Administrator, user.Profile);
        Assert.Equal(role, user.AdminRole);
    }

    [Fact]
    public void An_Administrator_Without_A_Role_Is_Refused_By_The_Factory()
    {
        Assert.Throws<InvalidOperationException>(() => Create(UserProfile.Administrator, null));
    }

    [Theory]
    [InlineData(UserProfile.Customer)]
    [InlineData(UserProfile.Employee)]
    public void A_Role_On_Any_Other_Profile_Is_Refused_By_The_Factory(UserProfile profile)
    {
        Assert.Throws<InvalidOperationException>(() => Create(profile, AdminRole.Support));
    }

    [Theory]
    [InlineData(UserProfile.Customer)]
    [InlineData(UserProfile.Employee)]
    public void A_Customer_Or_A_Cleaner_Carries_No_Role(UserProfile profile)
    {
        Assert.Null(Create(profile, null).AdminRole);
    }

    [Fact]
    public void SetAdminRole_Changes_An_Administrator_Role()
    {
        var user = Create(UserProfile.Administrator, AdminRole.Administrator);

        user.SetAdminRole(AdminRole.Accountant);

        Assert.Equal(AdminRole.Accountant, user.AdminRole);
    }

    [Fact]
    public void SetAdminRole_Refuses_A_Non_Administrator()
    {
        var customer = Create(UserProfile.Customer, null);

        Assert.Throws<InvalidOperationException>(() => customer.SetAdminRole(AdminRole.Support));
        Assert.Null(customer.AdminRole);
    }

    [Fact]
    public void The_Social_Factories_Create_Customers_Without_A_Role()
    {
        Assert.Null(User.CreateWithGoogle("g@example.com", "First", "Last", "google-sub").AdminRole);
        Assert.Null(User.CreateWithApple("a@example.com", "First", "Last", "apple-sub").AdminRole);
    }

    [Fact]
    public void Upgrading_A_Customer_To_A_Cleaner_Keeps_No_Role()
    {
        var user = Create(UserProfile.Customer, null);

        user.UpgradeToEmployee();

        Assert.Equal(UserProfile.Employee, user.Profile);
        Assert.Null(user.AdminRole);
    }
}
