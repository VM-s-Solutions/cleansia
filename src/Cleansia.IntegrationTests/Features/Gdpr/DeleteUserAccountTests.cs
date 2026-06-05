using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Gdpr;

[Collection("PostgresCollection")]
public class DeleteUserAccountTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    [Fact]
    public async Task DeleteUserAccount_AnonymizesUserAndCascadesToChildEntities()
    {
        await TestMethod(
            arrange: SeedUserWithChildEntities,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new DeleteUserAccount.Command());
            },
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess);

                var user = await context.Users.FirstAsync(u => u.Id == TestConstants.TestUserSession.TestUserId);
                Assert.Equal(AnonymizationMarker.Value, user.FirstName);
                Assert.Equal(AnonymizationMarker.Value, user.LastName);
                Assert.StartsWith("deleted_", user.Email);
                Assert.Null(user.PhoneNumber);
                Assert.Null(user.PreferredLanguageCode);
                Assert.False(user.IsActive);

                var consent = await context.UserConsents.FirstAsync(c => c.UserId == user.Id);
                Assert.False(consent.IsGranted);
                Assert.NotNull(consent.WithdrawnAt);

                var savedAddresses = await context.SavedAddresses.Where(a => a.UserId == user.Id).ToListAsync();
                Assert.Empty(savedAddresses);

                var auditEntry = await context.GdprRequests.FirstAsync(r => r.UserId == user.Id);
                Assert.Equal(GdprRequestStatus.Completed, auditEntry.Status);
            });
    }

    [Fact]
    public async Task DeleteUserAccount_BlockedByPendingDeletionRequest()
    {
        await TestMethod(
            arrange: async (CleansiaDbContext context) =>
            {
                await SeedConfirmedUser(context);
                var pending = GdprRequest.Create(TestConstants.TestUserSession.TestUserId, "Deletion");
                pending.MarkProcessing();
                context.GdprRequests.Add(pending);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new DeleteUserAccount.Command());
            },
            assert: (CleansiaDbContext _, BusinessResult result) =>
            {
                Assert.False(result.IsSuccess);
                Assert.Equal(BusinessErrorMessage.GdprDeletionAlreadyPending, result.Error!.Code);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task DeleteUserAccount_NoUser_ReturnsFailure()
    {
        await TestMethod<BusinessResult>(
            arrange: async (CleansiaDbContext context) =>
            {
                if (!await context.Languages.AnyAsync())
                {
                    context.Languages.Add(Language.Create("en", "English"));
                    await context.CommitAsync(CancellationToken.None);
                }
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new DeleteUserAccount.Command());
            },
            assert: (CleansiaDbContext _, BusinessResult result) =>
            {
                Assert.False(result.IsSuccess);
                Assert.Equal(BusinessErrorMessage.NotExistingUserWithEmail, result.Error!.Code);
                return Task.CompletedTask;
            });
    }

    private static async Task SeedUserWithChildEntities(CleansiaDbContext context)
    {
        await SeedConfirmedUser(context);

        var country = Country.Create("Czechia", "CZ");
        context.Countries.Add(country);
        // CommitAsync (not SaveChangesAsync) so the Auditable audit fields (CreatedBy) get stamped
        // from the test session — Countries.CreatedBy is NOT NULL and raw SaveChangesAsync skips stamping.
        await context.CommitAsync(CancellationToken.None);

        var consent = UserConsent.Grant(
            TestConstants.TestUserSession.TestUserId,
            ConsentType.MarketingEmails,
            ipAddress: null,
            userAgent: null);
        context.UserConsents.Add(consent);

        var address = Address.Create("Domazlicka 18", "Prague", "11000", country.Id);
        context.Addresses.Add(address);
        // CommitAsync (not SaveChangesAsync) so Addresses.CreatedBy (NOT NULL) gets audit-stamped.
        await context.CommitAsync(CancellationToken.None);

        var savedAddress = SavedAddress.Create(
            TestConstants.TestUserSession.TestUserId,
            address.Id,
            label: "Home",
            isDefault: true);
        context.SavedAddresses.Add(savedAddress);

        await context.CommitAsync(CancellationToken.None);
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
