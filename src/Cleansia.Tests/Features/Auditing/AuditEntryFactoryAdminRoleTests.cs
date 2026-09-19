using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.AdminUsers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.TestUtilities;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0066 D5 — the admin row records the role the act ran under, read from the session's
/// <c>admin_role</c> claim on the success and the failure arm alike; a session without the claim (a
/// token minted before roles existed) or with a claim that is not a role leaves the column null rather
/// than inventing one.
/// </summary>
public sealed class AuditEntryFactoryAdminRoleTests
{
    private static readonly AuditActionDescriptor SetRoleDescriptor = AuditActionDescriptor.For(typeof(SetAdminRole.Command));
    private static readonly SetAdminRole.Command Request = new("target-admin", AdminRole.Support);

    private static AuditEntryFactory Factory(IUserSessionProvider session) =>
        new(session, new TestRequestMetadataProvider(), new HostAudienceProvider(JwtAudiences.Admin));

    private static IUserSessionProvider AdminSession(string? roleClaim)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, UserProfile.Administrator.ToString()) };
        if (roleClaim is not null)
        {
            claims.Add(new Claim(AdminRoleSets.ClaimType, roleClaim));
        }

        return new TestUserSessionProvider("admin-1", "admin-1@cleansia.test", claims);
    }

    [Theory]
    [InlineData(AdminRole.Administrator)]
    [InlineData(AdminRole.Manager)]
    [InlineData(AdminRole.Support)]
    [InlineData(AdminRole.Accountant)]
    public void The_Success_Row_Carries_The_Role_The_Act_Ran_Under(AdminRole role)
    {
        var row = Factory(AdminSession(role.ToString())).CreateSuccess(Request, SetRoleDescriptor, snapshot: null);

        Assert.Equal(UserProfile.Administrator, row.ActorProfile);
        Assert.Equal(role, row.ActorAdminRole);
        Assert.Equal("admin.user.set_role", row.Action);
    }

    [Fact]
    public void The_Failure_Row_Carries_The_Role_Too()
    {
        var row = Factory(AdminSession(AdminRole.Support.ToString()))
            .CreateFailure(Request, SetRoleDescriptor, BusinessErrorMessage.CannotDemoteLastAdministrator);

        Assert.Equal(AdminRole.Support, row.ActorAdminRole);
        Assert.Equal(BusinessErrorMessage.CannotDemoteLastAdministrator, row.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Owner")]
    [InlineData("42")]
    public void A_Session_Without_A_Real_Role_Claim_Leaves_The_Column_Null(string? roleClaim)
    {
        var row = Factory(AdminSession(roleClaim)).CreateSuccess(Request, SetRoleDescriptor, snapshot: null);

        Assert.Equal(UserProfile.Administrator, row.ActorProfile);
        Assert.Null(row.ActorAdminRole);
    }
}
