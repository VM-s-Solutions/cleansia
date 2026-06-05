using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// GetUser ownership, BOTH directions. CanViewUserDetail is
/// OwnerOrElevated (ADR-0001 §D3): a non-admin may resolve ONLY their own user id. End-to-end on the
/// Partner host (UserController.GetById reads the UserId query param the OwnerOrElevated policy keys on,
/// with the GetUser.Handler ownership check as the inner backstop):
/// <list type="bullet">
///   <item>Employee requests a DIFFERENT user's id → denied (403 policy / 400 not-found), never the
///   other user's PII;</item>
///   <item>the owner requests their OWN id → 200 (the fix must not lock the owner out).</item>
/// </list>
/// </summary>
public sealed class Ac4GetUserOwnershipTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private sealed record TwoUsers(string SelfId, string SelfEmail, string OtherId);

    private async Task<TwoUsers> ArrangeTwoUsersAsync()
    {
        string selfId = "", otherId = "";
        const string selfEmail = "self@hosttests.local";

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var self = DomainSeed.EmployeeUser(selfEmail);
            var other = DomainSeed.Customer("other@hosttests.local");
            ctx.Users.AddRange(self, other);
            selfId = self.Id;
            otherId = other.Id;
        });

        return new TwoUsers(selfId, selfEmail, otherId);
    }

    [Fact]
    public async Task Employee_requesting_a_different_users_id_is_denied()
    {
        var u = await ArrangeTwoUsersAsync();
        // sub = self id, but we ask for the OTHER user's id.
        var token = TestJwtFactory.Mint(PartnerAudience, u.SelfId, u.SelfEmail, UserProfile.Employee);

        var resp = await PartnerClient(token).GetAsync($"/api/User/GetById?UserId={u.OtherId}");

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.NotExistingUserWithId);
    }

    [Fact]
    public async Task Owner_requesting_their_own_id_succeeds()
    {
        var u = await ArrangeTwoUsersAsync();
        var token = TestJwtFactory.Mint(PartnerAudience, u.SelfId, u.SelfEmail, UserProfile.Employee);

        var resp = await PartnerClient(token).GetAsync($"/api/User/GetById?UserId={u.SelfId}");

        HttpAssert.IsOk(resp);
    }

    [Fact]
    public async Task Admin_may_view_any_users_detail()
    {
        // Elevated == Admin: the policy short-circuits to allow, the handler skips the ownership gate.
        var u = await ArrangeTwoUsersAsync();
        var adminToken = TestJwtFactory.Mint(PartnerAudience, "admin-1", "admin@hosttests.local", UserProfile.Administrator);

        var resp = await PartnerClient(adminToken).GetAsync($"/api/User/GetById?UserId={u.OtherId}");

        HttpAssert.IsOk(resp);
    }
}
