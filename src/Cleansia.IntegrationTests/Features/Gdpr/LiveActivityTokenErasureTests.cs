using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.LiveActivities;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Gdpr;

/// <summary>
/// T-0587 — the Live Activity half of an erasure, through <c>GdprDeletionService</c>'s REAL query shape.
///
/// <para>Real Postgres for the tenancy: a <see cref="LiveActivityToken"/> is written by an authenticated
/// mobile route and therefore carries the SUBJECT's tenant, while an admin-driven erasure arrives with the
/// actor's — or with none. The walk has to read past the global filter, and a fixture whose rows are all
/// null-stamped passes over exactly that bug. The subject's rows here are stamped with a real tenant id the
/// erasing request does not carry.</para>
/// </summary>
[Collection("PostgresCollection")]
public class LiveActivityTokenErasureTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string RegistrationTenantId = "tenant-alpha";
    private const string SubjectDeviceId = "device-erase-la-int";
    private const string SubjectUpdateToken = "apns-update-token-erased-int";
    private const string SubjectPushToStartToken = "apns-pts-token-erased-int";

    private const string BystanderUserId = "user-keep-la-int";
    private const string BystanderToken = "apns-update-token-kept-int";

    [Fact]
    public async Task Erasure_Removes_Every_Registration_The_Subject_Holds_Even_Under_Another_Tenant()
    {
        await TestMethod(
            arrange: SeedUserWithRegistrations,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new DeleteUserAccount.Command());
            },
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess);

                var rows = await context.LiveActivityTokens.IgnoreQueryFilters().ToListAsync();

                Assert.DoesNotContain(rows, t => t.UserId == TestConstants.TestUserSession.TestUserId);
                Assert.DoesNotContain(rows, t => t.DeviceId == SubjectDeviceId);

                var kept = Assert.Single(rows);
                Assert.Equal(BystanderUserId, kept.UserId);
                Assert.Equal(BystanderToken, kept.Token);
            });
    }

    private static async Task SeedUserWithRegistrations(CleansiaDbContext context)
    {
        if (!await context.Languages.AnyAsync())
        {
            context.Languages.Add(Language.Create("en", "English"));
            await context.SaveChangesAsync();
        }

        var user = User.CreateWithPassword(
            email: TestConstants.TestUserSession.TestUserEmail,
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: TestConstants.TestUserSession.TestFirstName,
            lastName: TestConstants.TestUserSession.TestLastName);
        user.Id = TestConstants.TestUserSession.TestUserId;
        user.ConfirmEmail();
        context.Users.Add(user);

        // A real row, not just an id: LiveActivityTokens.UserId is a foreign key onto Users.
        var bystander = User.CreateWithPassword(
            email: "tomas.svoboda@cleansia.test",
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: "Tomas",
            lastName: "Svoboda");
        bystander.Id = BystanderUserId;
        bystander.ConfirmEmail();
        context.Users.Add(bystander);

        context.LiveActivityTokens.Add(LiveActivityToken.Create(
            TestConstants.TestUserSession.TestUserId,
            SubjectDeviceId,
            "order-la-int",
            SubjectUpdateToken,
            RegistrationTenantId));

        context.LiveActivityTokens.Add(LiveActivityToken.Create(
            TestConstants.TestUserSession.TestUserId,
            SubjectDeviceId,
            null,
            SubjectPushToStartToken,
            RegistrationTenantId));

        context.LiveActivityTokens.Add(LiveActivityToken.Create(
            BystanderUserId, "device-keep-la-int", "order-la-int-2", BystanderToken, tenantId: null));

        await context.CommitAsync(CancellationToken.None);
    }
}
