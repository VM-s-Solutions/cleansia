using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Tenancy;

/// <summary>
/// ADR-0064 D1 — a cleaner of a deactivated company is refused on the partner audiences and nowhere
/// else; an administrator is never refused (O-1); a customer never pays the registry read; and one
/// scope reads the registry once however many times it asks.
/// </summary>
public sealed class CompanySignInGateTests
{
    private const string TenantId = "cleansia-sk";

    private readonly Mock<ITenantRepository> _tenants = new();

    private CompanySignInGate Gate() => new(_tenants.Object);

    private void Registry(bool deactivated)
    {
        var tenant = Tenant.Create(TenantId, "Cleansia SK s.r.o.");
        if (deactivated)
        {
            tenant.Deactivate("admin-1", DateTimeOffset.UtcNow);
        }

        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
    }

    private static Cleansia.Core.Domain.Users.User Account(UserProfile profile, string? tenantId = TenantId)
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = profile });
        user.TenantId = tenantId;
        return user;
    }

    [Theory]
    [InlineData(JwtAudiences.Partner)]
    [InlineData(JwtAudiences.Mobile)]
    public async Task A_Cleaner_Of_A_Deactivated_Company_Is_Refused_On_A_Partner_Audience(string audience)
    {
        Registry(deactivated: true);

        var refusal = await Gate().RefusalForAsync(Account(UserProfile.Employee), audience, CancellationToken.None);

        Assert.Equal(BusinessErrorMessage.CompanyDeactivated, refusal);
    }

    [Theory]
    [InlineData(JwtAudiences.Partner)]
    [InlineData(JwtAudiences.Mobile)]
    public async Task A_Cleaner_Of_An_Operating_Company_Is_Admitted(string audience)
    {
        Registry(deactivated: false);

        Assert.Null(await Gate().RefusalForAsync(Account(UserProfile.Employee), audience, CancellationToken.None));
    }

    [Theory]
    [InlineData(JwtAudiences.Customer)]
    [InlineData(JwtAudiences.Admin)]
    public async Task A_Cleaner_Of_A_Deactivated_Company_Is_Admitted_Off_The_Partner_Audiences_Without_A_Registry_Read(string audience)
    {
        Registry(deactivated: true);

        Assert.Null(await Gate().RefusalForAsync(Account(UserProfile.Employee), audience, CancellationToken.None));
        _tenants.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(UserProfile.Administrator, JwtAudiences.Partner)]
    [InlineData(UserProfile.Administrator, JwtAudiences.Mobile)]
    [InlineData(UserProfile.Administrator, JwtAudiences.Admin)]
    [InlineData(UserProfile.Customer, JwtAudiences.Customer)]
    public async Task An_Administrator_Or_Customer_Of_A_Deactivated_Company_Is_Admitted_Without_A_Registry_Read(UserProfile profile, string audience)
    {
        Registry(deactivated: true);

        Assert.Null(await Gate().RefusalForAsync(Account(profile), audience, CancellationToken.None));
        _tenants.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_Account_With_No_Company_Is_Admitted()
    {
        Assert.Null(await Gate().RefusalForAsync(Account(UserProfile.Employee, tenantId: null), JwtAudiences.Partner, CancellationToken.None));
        _tenants.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Company_Missing_From_The_Registry_Does_Not_Refuse()
    {
        _tenants.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        Assert.Null(await Gate().RefusalForAsync(Account(UserProfile.Employee), JwtAudiences.Partner, CancellationToken.None));
    }

    [Fact]
    public async Task One_Scope_Reads_The_Registry_Once_For_The_Command_And_The_Mint()
    {
        Registry(deactivated: true);
        var gate = Gate();
        var user = Account(UserProfile.Employee);

        var first = await gate.RefusalForAsync(user, JwtAudiences.Partner, CancellationToken.None);
        var second = await gate.RefusalForAsync(user, JwtAudiences.Mobile, CancellationToken.None);

        Assert.Equal(BusinessErrorMessage.CompanyDeactivated, first);
        Assert.Equal(BusinessErrorMessage.CompanyDeactivated, second);
        _tenants.Verify(r => r.GetByIdAsync(TenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Only_The_Employee_Profile_Is_Refused_Until_A_Holding_Role_Exists()
    {
        Assert.Equal([UserProfile.Employee], CompanySignInGate.RefusedProfiles.ToArray());
    }
}
