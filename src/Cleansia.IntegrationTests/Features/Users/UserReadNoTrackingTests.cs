using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Users;

/// <summary>
/// PERF-IDA-08: the read-only user surfaces (GetUser, GetCurrentUser, GetUserByEmail) now read through
/// dedicated no-tracking variants, while the tracked <c>GetByEmailAsync</c>/<c>GetByIdAsync</c> stay
/// untouched for the mutation paths that share them. These pin that (a) the no-tracking variants return
/// the SAME row and do NOT enrol it in the change tracker, and (b) the tracked variants still track —
/// so flipping the wrong method would fail here.
/// </summary>
[Collection("PostgresCollection")]
public class UserReadNoTrackingTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    [Fact]
    public async Task GetByEmailNoTracking_ReturnsSameRow_Untracked()
    {
        await TestMethod<bool>(
            arrange: SeedConfirmedUser,
            act: async provider =>
            {
                var repo = provider.GetRequiredService<IUserRepository>();
                var context = provider.GetRequiredService<CleansiaDbContext>();

                var user = await repo.GetByEmailNoTrackingAsync(
                    TestConstants.TestUserSession.TestUserEmail, CancellationToken.None);

                Assert.NotNull(user);
                Assert.Equal(TestConstants.TestUserSession.TestUserId, user!.Id);
                Assert.Empty(context.ChangeTracker.Entries<User>());
                return true;
            },
            assert: (CleansiaDbContext _, bool ok) =>
            {
                Assert.True(ok);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task GetByEmail_TrackedVariant_StillTracks()
    {
        await TestMethod<bool>(
            arrange: SeedConfirmedUser,
            act: async provider =>
            {
                var repo = provider.GetRequiredService<IUserRepository>();
                var context = provider.GetRequiredService<CleansiaDbContext>();

                var user = await repo.GetByEmailAsync(
                    TestConstants.TestUserSession.TestUserEmail, CancellationToken.None);

                Assert.NotNull(user);
                Assert.Single(context.ChangeTracker.Entries<User>());
                return true;
            },
            assert: (CleansiaDbContext _, bool ok) =>
            {
                Assert.True(ok);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task GetByIdNoTracking_ReturnsSameRow_Untracked()
    {
        await TestMethod<bool>(
            arrange: SeedConfirmedUser,
            act: async provider =>
            {
                var repo = provider.GetRequiredService<IUserRepository>();
                var context = provider.GetRequiredService<CleansiaDbContext>();

                var user = await repo.GetByIdNoTrackingAsync(
                    TestConstants.TestUserSession.TestUserId, CancellationToken.None);

                Assert.NotNull(user);
                Assert.Equal(TestConstants.TestUserSession.TestUserEmail, user!.Email);
                Assert.Empty(context.ChangeTracker.Entries<User>());
                return true;
            },
            assert: (CleansiaDbContext _, bool ok) =>
            {
                Assert.True(ok);
                return Task.CompletedTask;
            });
    }

    private static async Task SeedConfirmedUser(CleansiaDbContext context)
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
        await context.CommitAsync(CancellationToken.None);
    }
}
