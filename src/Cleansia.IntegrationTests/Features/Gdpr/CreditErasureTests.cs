using System.Data.Common;
using System.Security.Claims;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Npgsql;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Gdpr;

/// <summary>
/// Successful account erasure forfeits each currency in the same PostgreSQL transaction as the
/// subject, with a reconcilable ledger. A filing, refusal or failed commit forfeits nothing.
/// Separate connections hold the two possible return/erasure orderings at explicit lock boundaries.
/// </summary>
[Collection("PostgresCollection")]
public class CreditErasureTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string SubjectId = TestConstants.TestUserSession.TestUserId;
    private const string AdminId = "admin-credit-erasure";
    private const string CzkId = "credit-erasure-czk";
    private const string EurId = "credit-erasure-eur";
    private const string EmptyCurrencyId = "credit-erasure-usd";
    private const string UnheldCurrencyId = "credit-erasure-gbp";
    private const string FailedRequestId = "credit-erasure-failed";
    private const string ReturnKey = "credit-erasure:return";
    private const string GrantKey = "credit-erasure:grant";
    private const string EndedOrderId = "credit-erasure-ended-order";
    private const string PaymentIntentId = "pi_credit_erasure";
    private const string ProfilePhoto = "credit-erasure/profile.jpg";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Theory]
    [InlineData("self", GdprAuditReasons.SelfDeletion)]
    [InlineData("admin", GdprAuditReasons.AdminDeletion)]
    [InlineData("retry", GdprAuditReasons.RetriedDeletion)]
    [InlineData("frozen", GdprAuditReasons.SelfDeletion)]
    [InlineData("cleaner-admin", GdprAuditReasons.AdminDeletion)]
    public async Task Successful_Erasure_Forfeits_Each_Currency_Once_And_Preserves_The_Ledger(string route, string reason)
    {
        await TestMethod(
            setup: route is "admin" or "retry" or "cleaner-admin" ? AsAdministrator : null,
            arrange: context => Seed(context, failedRequest: route == "retry", frozen: route == "frozen", cleaner: route == "cleaner-admin"),
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var result = route switch
                {
                    "admin" or "cleaner-admin" => await mediator.Send(new AdminDeleteUserAccount.Command(SubjectId)),
                    "retry" => await mediator.Send(new AdminRetryUserDeletion.Command(FailedRequestId)),
                    _ => await mediator.Send(new DeleteUserAccount.Command())
                };
                Assert.True(result.IsSuccess, result.Error?.Message);

                // Re-enter the real walk in a fresh scope. Whether the erased subject is refused or
                // walked again, no second forfeiture may be appended to any retained account.
                await using var repeat = provider.CreateAsyncScope();
                var repeated = await Erase(repeat.ServiceProvider);
                var repeatedContext = repeat.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                if (repeated.IsSuccess)
                    await repeatedContext.CommitAsync(CancellationToken.None);
                else
                    repeatedContext.Rollback();
                return result;
            },
            assert: async (CleansiaDbContext context, BusinessResult _) =>
            {
                await AssertForfeited(context, reason);
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.False(user.IsActive);
                Assert.EndsWith(User.AnonymisedEmailSuffix, user.Email);
                Assert.All(await context.GdprRequests.IgnoreQueryFilters().ToListAsync(),
                    request => Assert.Equal(GdprRequestStatus.Completed, request.Status));
            },
            transactional: false);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Cleaner_Filing_Or_Live_Order_Refusal_Does_Not_Forfeit_Credit(bool cleaner)
    {
        await TestMethod(
            arrange: context => Seed(context, cleaner: cleaner, liveOrder: !cleaner),
            act: provider => provider.GetRequiredService<IMediator>().Send(new DeleteUserAccount.Command()),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                if (cleaner)
                {
                    Assert.True(result.IsSuccess, result.Error?.Message);
                    var request = Assert.Single(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                    Assert.Equal(GdprRequestStatus.Pending, request.Status);
                }
                else
                {
                    Assert.True(result.IsFailure);
                    Assert.Equal(BusinessErrorMessage.GdprDeletionBlockedByOrder, result.Error!.Message);
                    Assert.Empty(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                }
                await AssertOriginalBalances(context);
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.True(user.IsActive);
                Assert.Equal(TestConstants.TestUserSession.TestUserEmail, user.Email);
            },
            transactional: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Failed_First_Attempt_Or_Retry_Rolls_Back_Forfeiture_And_Records_Failure(bool retry)
    {
        await TestMethod(
            setup: retry ? AsAdministrator : null,
            arrange: context => Seed(context, failedRequest: retry),
            act: async provider =>
            {
                provider.GetRequiredService<CleansiaDbContext>().AdminActionAudits.Add(new AdminActionAudit
                {
                    ActorId = new string('x', 40),
                    Action = "poison",
                    ActorProfile = UserProfile.Administrator,
                    Success = true,
                    TenantId = TestTenants.Default
                });
                var mediator = provider.GetRequiredService<IMediator>();
                // The commit fails, so nothing the walk staged lands, and the Failed row the outer
                // recorder writes from its own scope does.
                await Assert.ThrowsAnyAsync<DbUpdateException>(async () =>
                {
                    var attempt = retry
                        ? mediator.Send(new AdminRetryUserDeletion.Command(FailedRequestId))
                        : mediator.Send(new DeleteUserAccount.Command());
                    await attempt.WaitAsync(Timeout);
                });
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                await AssertOriginalBalances(context);
                var request = Assert.Single(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(GdprRequestStatus.Failed, request.Status);
                if (retry)
                    Assert.Equal(FailedRequestId, request.Id);
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.True(user.IsActive);
                Assert.Equal(TestConstants.TestUserSession.TestUserEmail, user.Email);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Walk_That_Throws_After_Locking_Releases_The_Owner_Lock_Before_Returning()
    {
        var thrower = new ThrowOnCreditReadAfterOwnerLock();
        await TestMethod(
            setup: services =>
            {
                services.AddDbContext<CleansiaDbContext>(options => options.AddInterceptors(thrower));
                return Task.CompletedTask;
            },
            arrange: context => Seed(context),
            act: async provider =>
            {
                thrower.Armed = true;
                var thrown = await Assert.ThrowsAnyAsync<Exception>(() =>
                    provider.GetRequiredService<IMediator>().Send(new DeleteUserAccount.Command()).WaitAsync(Timeout));
                Assert.Equal(ThrowOnCreditReadAfterOwnerLock.Message, thrown.Message);

                // The request's DbContext is still alive, so only the pipeline's rollback can have
                // released the lock by now.
                Assert.True(await OwnerLockIsFreeAsync(), "the failed walk still holds the owner lock");
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                await AssertOriginalBalances(context);
                var request = Assert.Single(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(GdprRequestStatus.Failed, request.Status);
                Assert.True((await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId)).IsActive);
            },
            transactional: false);
    }

    /// <summary>
    /// The owner lock blocks every return and grant for the subject, so it is taken after the walk's
    /// external calls, not before them.
    /// </summary>
    [Fact]
    public async Task The_Walk_Holds_No_Owner_Lock_During_Its_Blob_Deletes()
    {
        bool? ownerLockFreeDuringDelete = null;
        var blobs = new Mock<IBlobContainerClient>();
        blobs.Setup(b => b.DeleteAsync(ProfilePhoto, It.IsAny<CancellationToken>()))
            .Returns(async () => { ownerLockFreeDuringDelete = await OwnerLockIsFreeAsync(); });
        await TestMethod(
            setup: services =>
            {
                var factory = new Mock<IBlobContainerClientFactory>();
                factory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(blobs.Object);
                services.Replace(ServiceDescriptor.Singleton(factory.Object));
                return Task.CompletedTask;
            },
            arrange: async context =>
            {
                await Seed(context);
                (await context.Users.SingleAsync(u => u.Id == SubjectId)).UpdateProfilePhotoName(ProfilePhoto);
            },
            act: provider => provider.GetRequiredService<IMediator>().Send(new DeleteUserAccount.Command()),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.True(ownerLockFreeDuringDelete, "the owner lock was held across the blob delete");
                await AssertForfeited(context, GdprAuditReasons.SelfDeletion);
            },
            transactional: false);
    }

    [Fact]
    public async Task Erasure_With_No_Credit_Does_Not_Create_An_Account_Or_A_Ledger_Row()
    {
        await TestMethod(
            arrange: context => Seed(context, accounts: false),
            act: provider => provider.GetRequiredService<IMediator>().Send(new DeleteUserAccount.Command()),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Empty(await context.CreditAccounts.IgnoreQueryFilters().ToListAsync());
                Assert.Empty(await context.CreditTransactions.ToListAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task An_Ordinarily_Inactive_User_Can_Still_Receive_Returns_And_Grants()
    {
        await TestMethod(
            arrange: async context =>
            {
                await Seed(context);
                (await context.Users.SingleAsync(u => u.Id == SubjectId)).Deactivated("ordinary-deactivation", DateTimeOffset.UtcNow);
            },
            act: async provider =>
            {
                var repository = provider.GetRequiredService<ICreditAccountRepository>();
                Assert.True(await repository.TryReturnAsync(SubjectId, CzkId, 25m, ReturnKey, AdminId, CancellationToken.None));
                Assert.False(await repository.TryReturnAsync(SubjectId, CzkId, 25m, ReturnKey, AdminId, CancellationToken.None));
                var grantAccount = await repository.EnsureForUserAsync(SubjectId, UnheldCurrencyId, CancellationToken.None);
                Assert.NotNull(grantAccount);
                grantAccount.Issue(7m, CreditTransactionReason.Goodwill, "inactive-grant", AdminId);
                await provider.GetRequiredService<CleansiaDbContext>().CommitAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var accounts = await Accounts(context);
                Assert.Equal(4, accounts.Count);
                Assert.Equal(150m, accounts.Single(a => a.CurrencyId == CzkId).Balance);
                Assert.Equal(7m, accounts.Single(a => a.CurrencyId == UnheldCurrencyId).Balance);
                Assert.All(accounts, account => Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount)));
                Assert.DoesNotContain(accounts.SelectMany(a => a.Transactions), t => t.Reason == CreditTransactionReason.Expired);
            },
            transactional: false);
    }

    [Theory]
    [InlineData(CzkId)]
    [InlineData(UnheldCurrencyId)]
    public async Task Erasure_Winning_The_Owner_Lock_Suppresses_A_Late_Return_And_Any_New_Account(string currencyId)
    {
        await TestMethod(
            arrange: context => Seed(context),
            act: async provider =>
            {
                var context = provider.GetRequiredService<CleansiaDbContext>();
                Assert.True((await Erase(provider)).IsSuccess);
                await using (var observer = NewContext())
                    await AssertOriginalBalances(observer); // The staged forfeiture has not committed.

                var barrier = new OwnerLockAttempt();
                await using var returning = NewContext(barrier);
                var returnTask = new CreditAccountRepository(returning).TryReturnAsync(
                    SubjectId, currencyId, 25m, ReturnKey, AdminId, CancellationToken.None);
                await barrier.Attempted.Task.WaitAsync(Timeout);
                Assert.False(returnTask.IsCompleted);
                await context.CommitAsync(CancellationToken.None);
                Assert.False(await returnTask.WaitAsync(Timeout));

                await using var grantScope = provider.CreateAsyncScope();
                Assert.Null(await grantScope.ServiceProvider.GetRequiredService<ICreditAccountRepository>()
                    .EnsureForUserAsync(SubjectId, currencyId, CancellationToken.None));
                await grantScope.ServiceProvider.GetRequiredService<CleansiaDbContext>().CommitAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                await AssertForfeited(context, GdprAuditReasons.SelfDeletion);
                Assert.False(await context.CreditTransactions.AnyAsync(t => t.IdempotencyKey == ReturnKey));
            },
            transactional: false);
    }

    [Theory]
    [InlineData(CzkId)]
    [InlineData(UnheldCurrencyId)]
    public async Task A_Return_Winning_The_Owner_Lock_Is_Included_In_The_Later_Forfeiture(string currencyId)
    {
        var barrier = new OwnerLockAttempt();
        await TestMethod(
            setup: services =>
            {
                services.AddDbContext<CleansiaDbContext>(options => options.AddInterceptors(barrier));
                return Task.CompletedTask;
            },
            arrange: context => Seed(context),
            act: async provider =>
            {
                await using var returning = NewContext();
                await using var transaction = await returning.Database.BeginTransactionAsync();
                Assert.True(await new CreditAccountRepository(returning).TryReturnAsync(
                    SubjectId, currencyId, 25m, ReturnKey, AdminId, CancellationToken.None));

                var erasureTask = Erase(provider);
                await barrier.Attempted.Task.WaitAsync(Timeout);
                Assert.False(erasureTask.IsCompleted);
                await transaction.CommitAsync();
                Assert.True((await erasureTask.WaitAsync(Timeout)).IsSuccess);
                await provider.GetRequiredService<CleansiaDbContext>().CommitAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                await AssertForfeited(context, GdprAuditReasons.SelfDeletion, returnedCurrency: currencyId);
                var returned = await context.CreditTransactions.SingleAsync(t => t.IdempotencyKey == ReturnKey);
                Assert.Equal(25m, returned.Amount);
                Assert.Equal(CreditTransactionReason.OrderPaymentReturned, returned.Reason);
            },
            transactional: false);
    }

    [Theory]
    [InlineData(CzkId)]
    [InlineData(UnheldCurrencyId)]
    public async Task A_Grant_Waiting_On_An_Uncommitted_Erasure_Gets_No_Account_And_Writes_Nothing(string currencyId)
    {
        await TestMethod(
            arrange: context => Seed(context),
            act: async provider =>
            {
                var context = provider.GetRequiredService<CleansiaDbContext>();
                Assert.True((await Erase(provider)).IsSuccess);

                await using var granting = NewContext();
                await granting.Database.OpenConnectionAsync();
                var grantTask = new CreditAccountRepository(granting).EnsureForUserAsync(
                    SubjectId, currencyId, CancellationToken.None);
                await WaitUntilBlocked(BackendPid(granting), grantTask);
                Assert.False(grantTask.IsCompleted, "the grant must wait for the erasure's owner lock");

                await context.CommitAsync(CancellationToken.None);
                Assert.Null(await grantTask.WaitAsync(Timeout));
                await granting.CommitAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                await AssertForfeited(context, GdprAuditReasons.SelfDeletion);
                Assert.False(await context.CreditTransactions.AnyAsync(t => t.IdempotencyKey == GrantKey));
            },
            transactional: false);
    }

    [Theory]
    [InlineData(CzkId)]
    [InlineData(UnheldCurrencyId)]
    public async Task A_Grant_Holding_The_Owner_Lock_Is_Included_In_The_Later_Forfeiture(string currencyId)
    {
        await TestMethod(
            arrange: context => Seed(context),
            act: async provider =>
            {
                await using var granting = NewContext();
                var account = await new CreditAccountRepository(granting).EnsureForUserAsync(
                    SubjectId, currencyId, CancellationToken.None);
                Assert.NotNull(account);
                account.Issue(25m, CreditTransactionReason.Goodwill, GrantKey, AdminId);

                var context = provider.GetRequiredService<CleansiaDbContext>();
                await context.Database.OpenConnectionAsync();
                var erasureTask = Erase(provider);
                await WaitUntilBlocked(BackendPid(context), erasureTask);
                Assert.False(erasureTask.IsCompleted, "the erasure must wait for the grant's owner lock");

                await granting.CommitAsync(CancellationToken.None);
                Assert.True((await erasureTask.WaitAsync(Timeout)).IsSuccess);
                await context.CommitAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                await AssertForfeited(context, GdprAuditReasons.SelfDeletion, returnedCurrency: currencyId);
                var grant = await context.CreditTransactions.SingleAsync(t => t.IdempotencyKey == GrantKey);
                Assert.Equal(25m, grant.Amount);
            },
            transactional: false);
    }

    /// <summary>
    /// A refund after erasure gives back the card share only, once, whether the refund loaded its order
    /// before the erasure committed (and so still sees the owner) or after (and sees none).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Late_Or_Repeated_Mixed_Refund_After_Erasure_Refunds_The_Card_Share_Only(bool orderLoadedBeforeErasure)
    {
        var stripe = new Mock<IStripeClient>();
        await TestMethod(
            setup: services =>
            {
                var factory = new Mock<IStripeClientFactory>();
                factory.Setup(f => f.CreateClient()).Returns(stripe.Object);
                services.Replace(ServiceDescriptor.Singleton(factory.Object));
                return Task.CompletedTask;
            },
            arrange: context => Seed(context, endedCardOrder: true),
            act: async provider =>
            {
                await using var refundScope = provider.CreateAsyncScope();
                if (orderLoadedBeforeErasure)
                {
                    var stale = await refundScope.ServiceProvider.GetRequiredService<IOrderRepository>()
                        .GetByIdAsync(EndedOrderId, CancellationToken.None);
                    Assert.Equal(SubjectId, stale!.UserId);
                }

                Assert.True((await Erase(provider)).IsSuccess);
                await provider.GetRequiredService<CleansiaDbContext>().CommitAsync(CancellationToken.None);

                var request = new RefundRequest(EndedOrderId, 2000m, RefundReason.ServiceNotRendered, AdminId);
                var first = await refundScope.ServiceProvider.GetRequiredService<IRefundService>()
                    .IssueRefundAsync(request, CancellationToken.None);
                Assert.True(first.IsSuccess, first.Error?.Message);
                Assert.Equal(1500m, first.Value!.Amount);

                await using var retryScope = provider.CreateAsyncScope();
                var repeated = await retryScope.ServiceProvider.GetRequiredService<IRefundService>()
                    .IssueRefundAsync(request, CancellationToken.None);
                Assert.True(repeated.IsSuccess, repeated.Error?.Message);
                Assert.True(repeated.Value!.ResolvedToExisting);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                stripe.Verify(s => s.RefundPaymentIntentAsync(
                    PaymentIntentId, 1500m, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
                stripe.Verify(s => s.RefundPaymentIntentAsync(
                    It.IsAny<string>(), It.Is<decimal>(amount => amount != 1500m), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()), Times.Never);
                var refund = Assert.Single(await context.Refunds.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(RefundStatus.Succeeded, refund.Status);
                Assert.Equal(1500m, refund.Amount);
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == EndedOrderId);
                Assert.Null(order.UserId);
                Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
                Assert.False(await context.CreditTransactions.AnyAsync(
                    t => t.Reason == CreditTransactionReason.OrderPaymentReturned));
                Assert.Equal(0m, await new CreditAccountRepository(context)
                    .GetReturnedTotalForOrderAsync(EndedOrderId, CancellationToken.None));
                await AssertForfeited(context, GdprAuditReasons.SelfDeletion);
            },
            transactional: false);
    }

    [Fact]
    public async Task Erasure_Winning_The_Account_Lock_Refuses_A_Late_Raw_Debit_Without_A_Ledger_Movement()
    {
        const string debitKey = "credit-erasure:late-debit";
        await TestMethod(
            arrange: context => Seed(context),
            act: async provider =>
            {
                var context = provider.GetRequiredService<CleansiaDbContext>();
                var accountId = await context.CreditAccounts.AsNoTracking()
                    .Where(a => a.UserId == SubjectId && a.CurrencyId == CzkId)
                    .Select(a => a.Id).SingleAsync();
                Assert.True((await Erase(provider)).IsSuccess);

                var barrier = new RawDebitAttempt();
                await using var spending = NewContext(barrier);
                await spending.Database.OpenConnectionAsync();
                var debitTask = new CreditAccountRepository(spending).TryDebitAsync(
                    accountId, 25m, CreditTransactionReason.OrderPayment, debitKey, SubjectId, CancellationToken.None);
                await barrier.Attempted.Task.WaitAsync(Timeout);
                // Observe the database wait itself: without the account lock, a scheduler could let
                // the erasure's UPDATE beat the debit even though no lock protected the earlier read.
                await WaitUntilBlocked(((NpgsqlConnection)spending.Database.GetDbConnection()).ProcessID);
                Assert.False(debitTask.IsCompleted);
                await context.CommitAsync(CancellationToken.None);
                Assert.False(await debitTask.WaitAsync(Timeout));
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                await AssertForfeited(context, GdprAuditReasons.SelfDeletion);
                Assert.False(await context.CreditTransactions.AnyAsync(t => t.IdempotencyKey == debitKey));
            },
            transactional: false);
    }

    /// <summary>
    /// Returns once the backend waits on a lock, or once <paramref name="work"/> finished without ever
    /// waiting; the caller asserts which.
    /// </summary>
    private async Task WaitUntilBlocked(int backendPid, Task? work = null)
    {
        using var deadline = new CancellationTokenSource(Timeout);
        await using var observer = new NpgsqlConnection(Fixture.GetConnectionString());
        await observer.OpenAsync(deadline.Token);
        await using var command = new NpgsqlCommand("SELECT cardinality(pg_blocking_pids(@pid)) > 0", observer);
        command.Parameters.AddWithValue("pid", backendPid);
        while (await command.ExecuteScalarAsync(deadline.Token) is not true)
        {
            if (work is { IsCompleted: true })
                return;
            await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token);
        }
    }

    private async Task<bool> OwnerLockIsFreeAsync()
    {
        await using var probe = new NpgsqlConnection(Fixture.GetConnectionString());
        await probe.OpenAsync();
        await using var transaction = await probe.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(
            """SELECT "Id" FROM "Users" WHERE "Id" = @id FOR NO KEY UPDATE NOWAIT""", probe, transaction);
        command.Parameters.AddWithValue("id", SubjectId);
        try
        {
            await command.ExecuteScalarAsync();
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            return false;
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    private static int BackendPid(CleansiaDbContext context) =>
        ((NpgsqlConnection)context.Database.GetDbConnection()).ProcessID;

    private static Task<BusinessResult> Erase(IServiceProvider provider) =>
        provider.GetRequiredService<IGdprDeletionService>().DeleteUserAccountAsync(
            SubjectId, GdprAuditReasons.SelfDeletion, _ => (GdprAuditReasons.SelfActor, null),
            deferEmployeeErasure: false, CancellationToken.None);

    private CleansiaDbContext NewContext(DbCommandInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(Fixture.GetConnectionString());
        if (interceptor is not null)
            options.AddInterceptors(interceptor);
        return new CleansiaDbContext(options.Options,
            new TestUserSessionProvider(AdminId, "admin@cleansia.test"), new FixedTenantProvider(TestTenants.Default));
    }

    private static Task AsAdministrator(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            AdminId, "admin@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }

    private static Task<List<CreditAccount>> Accounts(CleansiaDbContext context) => context.CreditAccounts
        .IgnoreQueryFilters().AsNoTracking().Include(a => a.Transactions).Where(a => a.UserId == SubjectId).ToListAsync();

    private static async Task AssertOriginalBalances(CleansiaDbContext context)
    {
        var accounts = await Accounts(context);
        Assert.Equal(3, accounts.Count);
        Assert.Equal(125m, accounts.Single(a => a.CurrencyId == CzkId).Balance);
        Assert.Equal(12.50m, accounts.Single(a => a.CurrencyId == EurId).Balance);
        Assert.Equal(0m, accounts.Single(a => a.CurrencyId == EmptyCurrencyId).Balance);
        Assert.Equal(2, accounts.Sum(a => a.Transactions.Count));
        Assert.All(accounts, account => Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount)));
    }

    private static async Task AssertForfeited(CleansiaDbContext context, string reason, string? returnedCurrency = null)
    {
        var expected = new Dictionary<string, decimal> { [CzkId] = 125m, [EurId] = 12.50m, [EmptyCurrencyId] = 0m };
        if (returnedCurrency is not null)
            expected[returnedCurrency] = expected.GetValueOrDefault(returnedCurrency) + 25m;
        var accounts = await Accounts(context);
        Assert.Equal(expected.Count, accounts.Count);
        foreach (var account in accounts)
        {
            Assert.Equal(0m, account.Balance);
            Assert.Null(account.ExpiresOn);
            Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
            var forfeitures = account.Transactions.Where(t => t.Reason == CreditTransactionReason.Expired).ToList();
            if (expected[account.CurrencyId] == 0m)
            {
                Assert.Empty(forfeitures);
                Assert.Empty(account.Transactions);
                continue;
            }
            var forfeiture = Assert.Single(forfeitures);
            Assert.Equal(-expected[account.CurrencyId], forfeiture.Amount);
            Assert.Equal($"account-deletion:{account.Id}", forfeiture.IdempotencyKey);
            Assert.Equal($"Account deletion: {reason}", forfeiture.Note);
            Assert.Equal(GdprAuditReasons.SystemActor, forfeiture.CreatedBy);
        }
    }

    private static async Task Seed(CleansiaDbContext context, bool failedRequest = false,
        bool frozen = false, bool cleaner = false, bool liveOrder = false, bool accounts = true,
        bool endedCardOrder = false)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var subject = User.CreateWithPassword(TestConstants.TestUserSession.TestUserEmail,
            TestConstants.TestUserSession.TestUserPassword, "Credit", "Subject", cleaner ? UserProfile.Employee : UserProfile.Customer);
        subject.Id = SubjectId;
        subject.ConfirmEmail();
        context.Users.Add(subject);
        if (cleaner)
            context.Employees.Add(Employee.CreateWithUser(subject));
        foreach (var (id, code) in new[] { (CzkId, "CZK"), (EurId, "EUR"), (EmptyCurrencyId, "USD"), (UnheldCurrencyId, "GBP") })
        {
            var currency = Currency.Create(code, code, code);
            currency.Id = id;
            currency.IsActive = true;
            context.Currencies.Add(currency);
        }
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);

        if (accounts)
        {
            foreach (var (currency, amount) in new[] { (CzkId, 125m), (EurId, 12.50m), (EmptyCurrencyId, 0m) })
            {
                var account = CreditAccount.Create(SubjectId, currency, AdminId);
                if (amount > 0m)
                    account.Issue(amount, CreditTransactionReason.Goodwill, $"seed:{currency}", AdminId);
                context.CreditAccounts.Add(account);
            }
        }
        if (failedRequest)
        {
            var request = GdprRequest.Create(SubjectId, GdprRequest.DeletionRequestType);
            request.Id = FailedRequestId;
            request.MarkFailed(GdprAuditReasons.SelfActor, "Previous erasure failed");
            context.GdprRequests.Add(request);
        }
        if (liveOrder)
        {
            var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
            context.Countries.Add(country);
            var order = Order.Create("Credit Subject", TestConstants.TestUserSession.TestUserEmail,
                "+420777111333", Address.Create("Test 12", "Praha", "11000", country.Id), 1, 1,
                DateTime.UtcNow.AddDays(1), PaymentType.Cash, 1250m, CzkId, PaymentStatus.Pending, userId: SubjectId);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
            context.Orders.Add(order);
        }
        if (endedCardOrder)
        {
            var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
            context.Countries.Add(country);
            var order = Order.Create("Credit Subject", TestConstants.TestUserSession.TestUserEmail,
                "+420777111333", Address.Create("Test 12", "Praha", "11000", country.Id), 1, 1,
                DateTime.UtcNow.AddDays(-2), PaymentType.Card, 2000m, CzkId, PaymentStatus.Paid, userId: SubjectId);
            order.Id = EndedOrderId;
            order.ApplyCredit(500m, SubjectId);
            order.AssignStripePaymentIntentId(PaymentIntentId);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
            order.CompleteOrder(60);
            context.Orders.Add(order);
        }
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
        if (frozen)
        {
            var tenant = await context.Tenants.SingleAsync(t => t.Id == TestTenants.Default);
            var now = DateTimeOffset.UtcNow;
            tenant.RequestWindDown(DateOnly.FromDateTime(now.AddDays(-60).UtcDateTime), AdminId, now.AddDays(-60));
            tenant.Deactivate(AdminId, now.AddDays(-45));
            tenant.RequestArchive(AdminId, now);
            await context.CommitAsync(CancellationToken.None);
        }
    }

    private sealed class OwnerLockAttempt : DbCommandInterceptor
    {
        public TaskCompletionSource Attempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM \"Users\"", StringComparison.Ordinal)
                && command.CommandText.Contains("FOR NO KEY UPDATE", StringComparison.Ordinal))
                Attempted.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ThrowOnCreditReadAfterOwnerLock : DbCommandInterceptor
    {
        public const string Message = "Injected failure after the owner lock.";
        private bool locked;

        public bool Armed { get; set; }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && command.CommandText.Contains("FROM \"Users\"", StringComparison.Ordinal)
                && command.CommandText.Contains("FOR NO KEY UPDATE", StringComparison.Ordinal))
                locked = true;
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (locked && command.CommandText.Contains("FROM \"CreditAccounts\"", StringComparison.Ordinal))
            {
                Armed = false;
                locked = false;
                throw new InvalidOperationException(Message);
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RawDebitAttempt : DbCommandInterceptor
    {
        public TaskCompletionSource Attempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("WITH debited AS", StringComparison.Ordinal)
                && command.CommandText.Contains("UPDATE \"CreditAccounts\"", StringComparison.Ordinal))
                Attempted.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? currentTenantId = tenantId;
        public string? GetCurrentTenantId() => currentTenantId;
        public void SetTenantOverride(string tenantId) => currentTenantId = tenantId;
        public void ClearTenantOverride() => currentTenantId = null;
    }
}
