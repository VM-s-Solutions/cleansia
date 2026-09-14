using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Gdpr;

/// <summary>
/// Owner ruling 2026-09-14 (A3): a failed erasure is on record, retried, and visible. Against real
/// Postgres through the full MediatR pipeline: a commit that throws leaves exactly one <c>Failed</c>
/// deletion request — written out of band, so it survives the rolled-back walk — naming the cause and
/// never the subject's address, with the subject untouched; an admin retry completes that same row and
/// erases the subject; a retry that fails again keeps the row Failed with a second note and a fresh
/// last-attempt stamp; the daily sweep retries yesterday's failure and leaves today's alone; and a
/// refusal before the walk (a live order) leaves no row at all.
/// </summary>
[Collection("PostgresCollection")]
public class FailedErasureRecordTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string SubjectId = TestConstants.TestUserSession.TestUserId;
    private const string SubjectEmail = TestConstants.TestUserSession.TestUserEmail;
    private const string OtherSubjectId = "user-failed-erasure-2";
    private const string OtherSubjectEmail = "karel.prochazka@cleansia.test";
    private const string FailedRequestId = "01HZZFAILEDERASURE0000001";
    private const string OtherRequestId = "01HZZFAILEDERASURE0000002";
    private const string CountryId = "country-cz-failed-erasure";
    private const string CurrencyId = "currency-czk-failed-eras";
    private const string FirstNote = "DbUpdateException: boom";

    [Fact]
    public async Task A_Commit_Throw_Leaves_One_Failed_Row_Naming_The_Cause_And_The_Subject_Untouched()
    {
        await TestMethod(
            arrange: context => Seed(context),
            act: async provider =>
            {
                Poison(provider.GetRequiredService<CleansiaDbContext>());

                await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
                    provider.GetRequiredService<IMediator>().Send(new DeleteUserAccount.Command()));
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var request = Assert.Single(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(SubjectId, request.UserId);
                Assert.Equal(GdprRequest.DeletionRequestType, request.RequestType);
                Assert.Equal(GdprRequestStatus.Failed, request.Status);
                Assert.Equal(SubjectEmail, request.ProcessedBy);
                Assert.Equal(TestTenants.Default, request.TenantId);
                Assert.NotNull(request.CompletedAt);
                Assert.StartsWith("PostgresException:", request.Notes);
                Assert.DoesNotContain("@", request.Notes);
                Assert.DoesNotContain(TestConstants.TestUserSession.TestFirstName, request.Notes);

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.Equal(SubjectEmail, user.Email);
                Assert.True(user.IsActive);
                var consent = await context.UserConsents.IgnoreQueryFilters().SingleAsync(c => c.UserId == SubjectId);
                Assert.True(consent.IsGranted);
            },
            transactional: false);
    }

    [Fact]
    public async Task An_Admin_Retry_Completes_The_Failed_Row_Itself_And_Erases_The_Subject()
    {
        await TestMethod(
            arrange: context => Seed(context, failedRequest: true),
            act: provider => provider.GetRequiredService<IMediator>().Send(new AdminRetryUserDeletion.Command(FailedRequestId)),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                var request = Assert.Single(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(FailedRequestId, request.Id);
                Assert.Equal(GdprRequestStatus.Completed, request.Status);
                Assert.Equal(SubjectEmail, request.ProcessedBy);
                Assert.Equal($"{FirstNote}\nRetried by {SubjectEmail}", request.Notes);

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.StartsWith("deleted_", user.Email);
                Assert.False(user.IsActive);
                Assert.Equal(GdprAuditReasons.RetriedDeletion, user.DeactivatedBy);
                var consent = await context.UserConsents.IgnoreQueryFilters().SingleAsync(c => c.UserId == SubjectId);
                Assert.False(consent.IsGranted);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Retry_Whose_Commit_Throws_Again_Keeps_The_Row_Failed_With_A_Second_Note_And_A_Fresh_Stamp()
    {
        await TestMethod(
            arrange: context => Seed(context, failedRequest: true),
            act: async provider =>
            {
                Poison(provider.GetRequiredService<CleansiaDbContext>());

                await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
                    provider.GetRequiredService<IMediator>().Send(new AdminRetryUserDeletion.Command(FailedRequestId)));
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var request = Assert.Single(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(FailedRequestId, request.Id);
                Assert.Equal(GdprRequestStatus.Failed, request.Status);
                Assert.StartsWith($"{FirstNote}\nPostgresException:", request.Notes);
                Assert.DoesNotContain("@", request.Notes);
                Assert.NotNull(request.UpdatedOn);
                Assert.True(request.UpdatedOn > DateTimeOffset.UtcNow.AddMinutes(-1));

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.Equal(SubjectEmail, user.Email);
                Assert.True(user.IsActive);
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Sweep_Retries_Yesterdays_Failure_And_Leaves_Todays_Alone()
    {
        var startOfToday = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);

        await TestMethod(
            arrange: async context =>
            {
                await Seed(context, failedRequest: true, lastAttempt: startOfToday.AddMinutes(-1));
                await SeedOtherSubjectWithFailedRequest(context, lastAttempt: startOfToday.AddMinutes(1));
            },
            act: provider => provider.GetRequiredService<IMediator>().Send(new RetryFailedUserDeletions.Command()),
            assert: async (CleansiaDbContext context, BusinessResult<RetryFailedUserDeletions.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Equal(new RetryFailedUserDeletions.Response(1, 1, 0), result.Value);

                var retried = await context.GdprRequests.IgnoreQueryFilters().SingleAsync(r => r.Id == FailedRequestId);
                Assert.Equal(GdprRequestStatus.Completed, retried.Status);
                var erased = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.False(erased.IsActive);

                var waiting = await context.GdprRequests.IgnoreQueryFilters().SingleAsync(r => r.Id == OtherRequestId);
                Assert.Equal(GdprRequestStatus.Failed, waiting.Status);
                Assert.Equal(FirstNote, waiting.Notes);
                Assert.Equal(startOfToday.AddMinutes(1), waiting.UpdatedOn);
                var kept = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == OtherSubjectId);
                Assert.Equal(OtherSubjectEmail, kept.Email);
                Assert.True(kept.IsActive);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Refusal_Before_The_Walk_Leaves_No_Row()
    {
        await TestMethod(
            arrange: context => Seed(context, liveOrder: true),
            act: provider => provider.GetRequiredService<IMediator>().Send(new DeleteUserAccount.Command()),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsFailure);
                Assert.Equal(BusinessErrorMessage.GdprDeletionBlockedByOrder, result.Error!.Message);

                Assert.Empty(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.Equal(SubjectEmail, user.Email);
            },
            transactional: false);
    }

    // A row the commit cannot take (ActorId over its 26-char column) makes the pipeline's single
    // SaveChangesAsync throw after the whole walk has been staged.
    private static void Poison(CleansiaDbContext context) =>
        context.AdminActionAudits.Add(new AdminActionAudit
        {
            ActorId = new string('x', 40),
            Action = "poison",
            ActorProfile = UserProfile.Administrator,
            Success = true,
            TenantId = TestTenants.Default
        });

    private static async Task Seed(
        CleansiaDbContext context,
        bool failedRequest = false,
        bool liveOrder = false,
        DateTimeOffset? lastAttempt = null)
    {
        if (!await context.Languages.AnyAsync())
        {
            context.Languages.Add(Language.Create("en", "English"));
        }

        var subject = User.CreateWithPassword(
            email: SubjectEmail,
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: TestConstants.TestUserSession.TestFirstName,
            lastName: TestConstants.TestUserSession.TestLastName);
        subject.Id = SubjectId;
        subject.ConfirmEmail();
        context.Users.Add(subject);
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);

        context.UserConsents.Add(UserConsent.Grant(SubjectId, ConsentType.MarketingEmails, ipAddress: null, userAgent: null, documentVersion: null));

        if (failedRequest)
        {
            context.GdprRequests.Add(FailedRequest(FailedRequestId, SubjectId, lastAttempt));
        }

        if (liveOrder)
        {
            var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
            country.Id = CountryId;
            context.Countries.Add(country);
            var currency = Currency.Create("CZK", "Kč", "Czech koruna");
            currency.Id = CurrencyId;
            currency.IsActive = true;
            currency.SetAsDefault(true);
            context.Currencies.Add(currency);

            var order = Order.Create(
                customerName: $"{TestConstants.TestUserSession.TestFirstName} {TestConstants.TestUserSession.TestLastName}",
                customerEmail: SubjectEmail,
                customerPhone: "+420777111333",
                customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
                rooms: 2,
                bathrooms: 1,
                cleaningDateTime: DateTime.UtcNow.AddHours(3),
                paymentType: PaymentType.Cash,
                totalPrice: 1250m,
                currencyId: CurrencyId,
                paymentStatus: PaymentStatus.Pending,
                userId: SubjectId);
            order.Id = "order-failed-erasure-live";
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.InProgress, order));
            context.Orders.Add(order);
        }

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private static async Task SeedOtherSubjectWithFailedRequest(CleansiaDbContext context, DateTimeOffset lastAttempt)
    {
        var other = User.CreateWithPassword(OtherSubjectEmail, "Seed-Password-123", "Karel", "Prochazka");
        other.Id = OtherSubjectId;
        other.ConfirmEmail();
        context.Users.Add(other);
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);

        context.GdprRequests.Add(FailedRequest(OtherRequestId, OtherSubjectId, lastAttempt));
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private static GdprRequest FailedRequest(string id, string userId, DateTimeOffset? lastAttempt)
    {
        var request = GdprRequest.Create(userId, GdprRequest.DeletionRequestType);
        request.Id = id;
        request.MarkFailed(userId, FirstNote);
        request.Created("seed", DateTimeOffset.UtcNow.AddDays(-2));
        if (lastAttempt is { } attempt)
        {
            request.Updated("seed", attempt);
        }

        return request;
    }
}
