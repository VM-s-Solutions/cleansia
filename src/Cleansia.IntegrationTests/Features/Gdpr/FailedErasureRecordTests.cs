using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Gdpr;

/// <summary>
/// Owner ruling 2026-09-14 (A3): a failed erasure is on record, retried, and visible. Against real
/// Postgres through the full MediatR pipeline: a commit that throws leaves exactly one <c>Failed</c>
/// deletion request — written out of band, so it survives the rolled-back walk — naming the cause and
/// never the subject's address, with the subject untouched; an admin retry completes that same row,
/// erases the subject and is an audited admin act; a retry that fails again keeps the row Failed with a
/// second note and a fresh last-attempt stamp; the daily sweep, run the way the timer runs it — no
/// session, no ambient tenant — retries yesterday's failure and leaves today's alone, and where the retry
/// throws or is refused it leaves the row Failed under its own tenant with the second note and the
/// system actor on it; and a refusal before the walk (a live order) leaves no row at all. A retry the
/// sweep fails tells the row's own company through the admin feed and the send-email outbox — once
/// that day, because the day's second run selects no candidate, and again the next day under a subject
/// naming the new day.
/// </summary>
[Collection("PostgresCollection")]
public class FailedErasureRecordTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string SubjectId = TestConstants.TestUserSession.TestUserId;
    private const string SubjectEmail = TestConstants.TestUserSession.TestUserEmail;
    private const string OtherSubjectId = "user-failed-erasure-2";
    private const string OtherSubjectEmail = "karel.prochazka@cleansia.test";
    private const string HandleHolderId = "user-failed-erasure-3";
    private const string AdminId = "admin-failed-erasure-1";
    private const string AdminEmail = "admin@cleansia.test";
    private const string FailedRequestId = "01HZZFAILEDERASURE0000001";
    private const string OtherRequestId = "01HZZFAILEDERASURE0000002";
    private const string CountryId = "country-cz-failed-erasure";
    private const string CurrencyId = "currency-czk-failed-eras";
    private const string FirstNote = "DbUpdateException: boom";
    private const string AdminOfSecondId = "admin-failed-erasure-sk";
    private const string AdminOfDefaultId = "admin-failed-erasure-cz";

    private static readonly DateTimeOffset StartOfToday = new(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);

    private static Task AsAdministrator(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            AdminId, AdminEmail, [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }

    // The Functions host: no JWT behind the session, and no claim behind the tenant provider — the row's
    // own tenant, set per candidate by the sweep, is the only tenant any read or write inside it has.
    private static Task AsTheTimer(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider()));
        services.Replace(ServiceDescriptor.Scoped<ITenantProvider>(sp =>
            new TenantProvider(sp.GetRequiredService<IHttpContextAccessor>())));
        return Task.CompletedTask;
    }

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
                Assert.Equal(GdprAuditReasons.SelfActor, request.ProcessedBy);
                Assert.DoesNotContain("@", request.ProcessedBy);
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
    public async Task An_Admin_Retry_Completes_The_Failed_Row_Itself_Erases_The_Subject_And_Is_Audited()
    {
        await TestMethod(
            setup: AsAdministrator,
            arrange: context => Seed(context, failedRequest: true),
            act: provider => provider.GetRequiredService<IMediator>().Send(new AdminRetryUserDeletion.Command(FailedRequestId)),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                var request = Assert.Single(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(FailedRequestId, request.Id);
                Assert.Equal(GdprRequestStatus.Completed, request.Status);
                Assert.Equal(AdminEmail, request.ProcessedBy);
                Assert.Equal($"{FirstNote}\nRetried by {AdminEmail}", request.Notes);

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.StartsWith("deleted_", user.Email);
                Assert.False(user.IsActive);
                Assert.Equal(GdprAuditReasons.RetriedDeletion, user.DeactivatedBy);
                var consent = await context.UserConsents.IgnoreQueryFilters().SingleAsync(c => c.UserId == SubjectId);
                Assert.False(consent.IsGranted);

                var audit = Assert.Single(await context.AdminActionAudits.IgnoreQueryFilters().ToListAsync());
                Assert.Equal("gdpr.user.delete.retry", audit.Action);
                Assert.True(audit.Success);
                Assert.Equal("GdprRequest", audit.ResourceType);
                Assert.Equal(FailedRequestId, audit.ResourceId);
                Assert.Equal(AdminId, audit.ActorId);
                Assert.DoesNotContain(SubjectEmail, audit.AfterJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            },
            transactional: false);
    }

    [Fact]
    public async Task An_Admin_Retry_Whose_Commit_Throws_Again_Keeps_The_Row_Failed_With_A_Second_Note_And_A_Fresh_Stamp()
    {
        await TestMethod(
            setup: AsAdministrator,
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
                Assert.Equal(AdminEmail, request.ProcessedBy);
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
    public async Task The_Sweep_Retries_Yesterdays_Failure_As_The_System_Actor_And_Leaves_Todays_Alone()
    {
        await TestMethod(
            setup: AsTheTimer,
            arrange: async context =>
            {
                await Seed(context, failedRequest: true, lastAttempt: StartOfToday.AddMinutes(-1));
                await SeedOtherSubjectWithFailedRequest(context, lastAttempt: StartOfToday.AddMinutes(1));
            },
            act: provider => provider.GetRequiredService<IMediator>().Send(new RetryFailedUserDeletions.Command()),
            assert: async (CleansiaDbContext context, BusinessResult<RetryFailedUserDeletions.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Equal(new RetryFailedUserDeletions.Response(1, 1, 0), result.Value);

                var retried = await context.GdprRequests.IgnoreQueryFilters().SingleAsync(r => r.Id == FailedRequestId);
                Assert.Equal(GdprRequestStatus.Completed, retried.Status);
                Assert.Equal(GdprAuditReasons.SystemActor, retried.ProcessedBy);
                Assert.Equal($"{FirstNote}\nRetried by {GdprAuditReasons.SystemActor}", retried.Notes);
                Assert.Equal(TestTenants.Default, retried.TenantId);
                var erased = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.False(erased.IsActive);
                Assert.Equal(GdprAuditReasons.RetriedDeletion, erased.DeactivatedBy);
                Assert.Empty(await context.AdminActionAudits.IgnoreQueryFilters().ToListAsync());

                var waiting = await context.GdprRequests.IgnoreQueryFilters().SingleAsync(r => r.Id == OtherRequestId);
                Assert.Equal(GdprRequestStatus.Failed, waiting.Status);
                Assert.Equal(FirstNote, waiting.Notes);
                Assert.Equal(StartOfToday.AddMinutes(1), waiting.UpdatedOn);
                var kept = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == OtherSubjectId);
                Assert.Equal(OtherSubjectEmail, kept.Email);
                Assert.True(kept.IsActive);
            },
            transactional: false);
    }

    // The candidate's own commit is what throws here, inside a scope the test never sees: a bystander
    // already holds the handle User.Anonymize would give the subject, and Users.Email is unique across
    // the holding. The row lives under the SECOND tenant so that only the tenant the sweep sets per
    // candidate can find it — a no-op override would answer not-found and write no second note.
    [Fact]
    public async Task The_Sweep_Leaves_A_Row_Whose_Commit_Throws_Again_Failed_With_A_Second_Note_Under_Its_Own_Tenant()
    {
        await TestMethod(
            setup: AsTheTimer,
            arrange: context => Seed(context, failedRequest: true, handleTaken: true, lastAttempt: StartOfToday.AddMinutes(-1), tenant: TestTenants.Second),
            act: provider => provider.GetRequiredService<IMediator>().Send(new RetryFailedUserDeletions.Command()),
            assert: async (CleansiaDbContext context, BusinessResult<RetryFailedUserDeletions.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Equal(new RetryFailedUserDeletions.Response(1, 0, 1), result.Value);

                var request = Assert.Single(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(FailedRequestId, request.Id);
                Assert.Equal(GdprRequestStatus.Failed, request.Status);
                Assert.StartsWith($"{FirstNote}\nPostgresException: 23505", request.Notes);
                Assert.DoesNotContain("@", request.Notes);
                Assert.Equal(GdprAuditReasons.SystemActor, request.ProcessedBy);
                Assert.Equal(TestTenants.Second, request.TenantId);
                Assert.NotNull(request.UpdatedOn);
                Assert.True(request.UpdatedOn > DateTimeOffset.UtcNow.AddMinutes(-1));

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.Equal(SubjectEmail, user.Email);
                Assert.True(user.IsActive);
                var consent = await context.UserConsents.IgnoreQueryFilters().SingleAsync(c => c.UserId == SubjectId);
                Assert.True(consent.IsGranted);
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Sweep_Tells_The_Rows_Company_When_The_Retry_Fails_Again_And_The_Days_Second_Run_Selects_Nothing()
    {
        await TestMethod(
            setup: AsTheTimer,
            arrange: async context =>
            {
                await Seed(context, failedRequest: true, handleTaken: true, lastAttempt: StartOfToday.AddMinutes(-1), tenant: TestTenants.Second);
                await SeedAdministrators(context);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var first = await mediator.Send(new RetryFailedUserDeletions.Command());
                var second = await mediator.Send(new RetryFailedUserDeletions.Command());
                return (First: first, Second: second);
            },
            assert: async (CleansiaDbContext context, (BusinessResult<RetryFailedUserDeletions.Response> First, BusinessResult<RetryFailedUserDeletions.Response> Second) swept) =>
            {
                Assert.Equal(new RetryFailedUserDeletions.Response(1, 0, 1), swept.First.Value);
                Assert.Equal(new RetryFailedUserDeletions.Response(0, 0, 0), swept.Second.Value);

                var today = StartOfToday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var row = Assert.Single(await ErasureFailedRows(context));
                Assert.Equal(AdminOfSecondId, row.UserId);
                Assert.Equal(TestTenants.Second, row.TenantId);
                var args = JsonSerializer.Deserialize<Dictionary<string, string>>(row.ArgsJson)!;
                Assert.Equal(FailedRequestId, args["requestId"]);
                Assert.Equal(today, args["day"]);
                Assert.Equal(2, args.Count);
                Assert.DoesNotContain("@", row.ArgsJson);

                var email = Assert.Single(await ErasureFailedEmails(context));
                Assert.Equal(TestTenants.Second, email.TenantId);
                var envelope = Read(email);
                Assert.Equal($"{AdminOfSecondId}@cleansia.test", envelope.Payload.Email);
                Assert.Equal($"{FailedRequestId}:{today}", envelope.Payload.Subject);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Request_That_Fails_Again_Tomorrow_Tells_The_Company_Again_Under_The_New_Day()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        await TestMethod(
            setup: async services =>
            {
                await AsTheTimer(services);
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock));
            },
            arrange: async context =>
            {
                await Seed(context, failedRequest: true, handleTaken: true, lastAttempt: StartOfToday.AddMinutes(-1), tenant: TestTenants.Second);
                await SeedAdministrators(context);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var today = await mediator.Send(new RetryFailedUserDeletions.Command());
                clock.Advance(TimeSpan.FromDays(1));
                var tomorrow = await mediator.Send(new RetryFailedUserDeletions.Command());
                return (Today: today, Tomorrow: tomorrow);
            },
            assert: async (CleansiaDbContext context, (BusinessResult<RetryFailedUserDeletions.Response> Today, BusinessResult<RetryFailedUserDeletions.Response> Tomorrow) swept) =>
            {
                Assert.Equal(new RetryFailedUserDeletions.Response(1, 0, 1), swept.Today.Value);
                Assert.Equal(new RetryFailedUserDeletions.Response(1, 0, 1), swept.Tomorrow.Value);

                var days = new[] { StartOfToday, StartOfToday.AddDays(1) }
                    .Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                    .ToList();
                var rows = await ErasureFailedRows(context);
                Assert.Equal(2, rows.Count);
                Assert.All(rows, r => Assert.Equal(AdminOfSecondId, r.UserId));
                Assert.Equal(days, rows.Select(r => JsonSerializer.Deserialize<Dictionary<string, string>>(r.ArgsJson)!["day"]).Order());

                var emails = await ErasureFailedEmails(context);
                Assert.Equal(2, emails.Count);
                Assert.Equal(2, emails.Select(e => e.MessageKey).Distinct(StringComparer.Ordinal).Count());
                Assert.Equal(days.Select(d => $"{FailedRequestId}:{d}"), emails.Select(e => Read(e).Payload.Subject).Order());
            },
            transactional: false);
    }

    private static Task<List<UserNotification>> ErasureFailedRows(CleansiaDbContext context) =>
        context.Set<UserNotification>().IgnoreQueryFilters()
            .Where(n => n.EventKey == AdminNotificationEventCatalog.ErasureFailed)
            .OrderBy(n => n.CreatedOn)
            .ToListAsync();

    private static Task<List<Core.Domain.Outbox.OutboxMessage>> ErasureFailedEmails(CleansiaDbContext context) =>
        context.OutboxMessages.IgnoreQueryFilters()
            .Where(m => m.QueueName == QueueNames.SendEmail && m.Body.Contains(AdminNotificationEventCatalog.ErasureFailed))
            .OrderBy(m => m.CreatedOn)
            .ToListAsync();

    private static QueueEnvelope<SendAdminNotificationEmailMessage> Read(Core.Domain.Outbox.OutboxMessage row) =>
        JsonSerializer.Deserialize<QueueEnvelope<SendAdminNotificationEmailMessage>>(
            row.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;

    private static async Task SeedAdministrators(CleansiaDbContext context)
    {
        var ofSecond = User.CreateWithPassword($"{AdminOfSecondId}@cleansia.test", "Seed-Password-123", "Ad", "Min", UserProfile.Administrator);
        ofSecond.Id = AdminOfSecondId;
        ofSecond.TenantId = TestTenants.Second;
        ofSecond.ConfirmEmail();
        var ofDefault = User.CreateWithPassword($"{AdminOfDefaultId}@cleansia.test", "Seed-Password-123", "Ad", "Min", UserProfile.Administrator);
        ofDefault.Id = AdminOfDefaultId;
        ofDefault.TenantId = TestTenants.Default;
        ofDefault.ConfirmEmail();
        context.Users.AddRange(ofSecond, ofDefault);
        await context.CommitAsync(CancellationToken.None);
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    [Fact]
    public async Task The_Sweep_Stamps_A_Refusal_On_The_Row_And_Counts_It_Failed()
    {
        await TestMethod(
            setup: AsTheTimer,
            arrange: context => Seed(context, failedRequest: true, liveOrder: true, lastAttempt: StartOfToday.AddMinutes(-1)),
            act: provider => provider.GetRequiredService<IMediator>().Send(new RetryFailedUserDeletions.Command()),
            assert: async (CleansiaDbContext context, BusinessResult<RetryFailedUserDeletions.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Equal(new RetryFailedUserDeletions.Response(1, 0, 1), result.Value);

                var request = Assert.Single(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(FailedRequestId, request.Id);
                Assert.Equal(GdprRequestStatus.Failed, request.Status);
                Assert.Equal($"{FirstNote}\n{BusinessErrorMessage.GdprDeletionBlockedByOrder}", request.Notes);
                Assert.Equal(GdprAuditReasons.SystemActor, request.ProcessedBy);
                Assert.True(request.UpdatedOn > DateTimeOffset.UtcNow.AddMinutes(-1));

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.Equal(SubjectEmail, user.Email);
                Assert.True(user.IsActive);
            },
            transactional: false);
    }

    // The subject files again while their failed erasure sits on record. The Failed row is the sweep's
    // (or an admin's) to finish, so the second filing is refused as already pending; were it to complete,
    // the sweep would then re-walk the erased subject through the first row and two Completed rows would
    // stand for one erasure. The session is the subject's for the filing and the timer's for the sweep.
    [Fact]
    public async Task A_Failed_Row_Refuses_A_Second_Filing_And_The_Sweep_Completes_The_Original()
    {
        IUserSessionProvider session = new TestUserSessionProvider(new TestClaimsPrincipalUser());

        await TestMethod(
            setup: services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => session));
                return Task.CompletedTask;
            },
            arrange: context => Seed(context, failedRequest: true, lastAttempt: StartOfToday.AddMinutes(-1)),
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var refiled = await mediator.Send(new DeleteUserAccount.Command());

                session = new TestUserSessionProvider();
                var swept = await mediator.Send(new RetryFailedUserDeletions.Command());
                return (Refiled: refiled, Swept: swept);
            },
            assert: async (CleansiaDbContext context, (BusinessResult Refiled, BusinessResult<RetryFailedUserDeletions.Response> Swept) outcome) =>
            {
                Assert.True(outcome.Refiled.IsFailure);
                Assert.Equal(BusinessErrorMessage.GdprDeletionAlreadyPending, outcome.Refiled.Error!.Message);

                Assert.True(outcome.Swept.IsSuccess, outcome.Swept.Error?.Message);
                Assert.Equal(new RetryFailedUserDeletions.Response(1, 1, 0), outcome.Swept.Value);

                var request = Assert.Single(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(FailedRequestId, request.Id);
                Assert.Equal(GdprRequestStatus.Completed, request.Status);
                Assert.Equal(GdprAuditReasons.SystemActor, request.ProcessedBy);
                Assert.Equal($"{FirstNote}\nRetried by {GdprAuditReasons.SystemActor}", request.Notes);

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.False(user.IsActive);
                Assert.StartsWith("deleted_", user.Email);
                Assert.Equal(GdprAuditReasons.RetriedDeletion, user.DeactivatedBy);
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
        bool handleTaken = false,
        DateTimeOffset? lastAttempt = null,
        string tenant = TestTenants.Default)
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

        if (handleTaken)
        {
            var holder = User.CreateWithPassword($"deleted_{SubjectId}@anonymized.local", "Seed-Password-123", "Holder", "Of-The-Handle");
            holder.Id = HandleHolderId;
            context.Users.Add(holder);
        }

        StampUnstampedAdded(context, tenant);
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

        StampUnstampedAdded(context, tenant);
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
        request.MarkFailed(GdprAuditReasons.SelfActor, FirstNote);
        request.Created("seed", DateTimeOffset.UtcNow.AddDays(-2));
        if (lastAttempt is { } attempt)
        {
            request.Updated("seed", attempt);
        }

        return request;
    }
}
