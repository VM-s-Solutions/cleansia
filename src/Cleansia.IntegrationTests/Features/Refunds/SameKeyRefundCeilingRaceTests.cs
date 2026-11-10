using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.AppServices.Behaviors;
using System.Data.Common;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Refunds;

[Collection("PostgresCollection")]
public class SameKeyRefundCeilingRaceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-probe";
    private const string CzkId = "cur-czk-probe";
    private const string OrderId = "order-probe";
    private const string DisputeId = "dispute-probe";
    private const string PaymentIntentId = "pi_probe";
    private const string AdminId = "admin-probe";
    private const string DisputeKey = $"refund:{OrderId}:dispute:{DisputeId}";
    private const string WindDownKey = $"refund:{OrderId}:admin";

    private readonly Mock<IStripeClient> _stripe = new();
    private readonly List<(decimal Amount, string Key)> _stripeRefunds = [];

    private CleansiaDbContext NewContext(ITenantProvider? tenants = null, params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .AddInterceptors(interceptors)
            .Options,
        new TestUserSessionProvider(AdminId, "admin@cleansia.test"),
        tenants ?? new FixedTenantProvider(TestTenants.Default));

    private async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToExclude = ["pg_catalog", "information_schema"]
        });
        await respawner.ResetAsync(conn);
        await SeedTenantRegistryAsync(conn);

        _stripe.Setup(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, decimal, string, CancellationToken>((_, amount, key, _) => _stripeRefunds.Add((amount, key)))
            .Returns(Task.CompletedTask);
    }

    /// <summary>A paid card order of 1000 and one claim on <paramref name="key"/> of 1000 whose Stripe answer is still out.</summary>
    private async Task SeedAsync(string key, RefundReason reason, bool withDispute, bool cancelled, decimal cardAmount = 1000m, decimal settledCredit = 0m)
    {
        await using var ctx = NewContext();
        ctx.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        ctx.Countries.Add(country);
        var currency = Currency.Create("CZK", "CZK", "CZK");
        currency.Id = CzkId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        ctx.Currencies.Add(currency);

        var user = User.CreateWithPassword("ceiling-race@cleansia.test", "Password123!", "Ceiling", "Race");
        user.Id = "user-ceiling-race";
        ctx.Users.Add(user);
        var order = Order.Create(
            customerName: "Probe",
            customerEmail: "probe@example.test",
            customerPhone: "+420000000009",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-3),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: CzkId,
            paymentStatus: PaymentStatus.Paid,
            userId: user.Id,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.AssignStripePaymentIntentId(PaymentIntentId);
        if (cancelled)
        {
            order.Cancel(DateTime.UtcNow.AddHours(-3), CancelledBy.System, 0m, 1000m, OrderCancellationReasons.CompanyWindDown);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
        }
        else
        {
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        }

        ctx.Orders.Add(order);

        if (withDispute)
        {
            var dispute = new Dispute(OrderId, null, DisputeReason.QualityIssue, "Probe dispute", AdminId);
            dispute.Id = DisputeId;
            ctx.Disputes.Add(dispute);
        }

        var claim = Refund.Create(OrderId, key, cardAmount, "CZK", reason, RefundSource.AppRefund,
            disputeId: withDispute ? DisputeId : null);
        claim.Created("seed", DateTimeOffset.UtcNow.AddHours(-2));
        ctx.Refunds.Add(claim);
        await ctx.CommitAsync(CancellationToken.None);
        if (settledCredit > 0m)
        {
            var account = await new CreditAccountRepository(ctx).EnsureForUserIgnoringTenantAsync(user.Id, CzkId, CancellationToken.None);
            account!.Issue(settledCredit, CreditTransactionReason.DisputeSettlement, "credit-settlement:earlier",
                AdminId, orderId: OrderId);
            await ctx.CommitAsync(CancellationToken.None);
        }
    }

    private RefundService NewRefundService(CleansiaDbContext ctx)
    {
        var factory = new Mock<IStripeClientFactory>();
        factory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        return new RefundService(
            new RefundRepository(ctx),
            new OrderRepository(ctx),
            new CreditAccountRepository(ctx),
            factory.Object,
            Mock.Of<IAdminNotifier>(),
            new UserNotificationRepository(ctx),
            NullLogger<RefundService>.Instance);
    }

    /// <summary>The other settle of the same key, run to completion on a connection of its own: Stripe pays it and it commits Succeeded.</summary>
    private Func<Task> FirstSettleCompletes(RefundRequest request) => async () =>
    {
        await using var ctx = NewContext();
        var settled = await NewRefundService(ctx).IssueRefundAsync(request, CancellationToken.None);
        Assert.True(settled.IsSuccess, settled.Error?.Message);
    };

    private async Task<(Order Order, Refund Refund, Dispute? Dispute)> ReadAsync(string key)
    {
        await using var ctx = NewContext();
        var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == OrderId);
        var refund = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking().SingleAsync(r => r.RefundKey == key);
        var dispute = await ctx.Disputes.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(d => d.Id == DisputeId);
        return (order, refund, dispute);
    }

    [Fact]
    public async Task A_Concurrent_Dispute_Resolution_Preserves_The_Paid_Refund()
    {
        await ResetAsync();
        await SeedAsync(DisputeKey, RefundReason.DisputeResolution, withDispute: true, cancelled: false);

        var interceptor = new AtFirstRefundSum(FirstSettleCompletes(
            new RefundRequest(OrderId, 1000m, RefundReason.DisputeResolution, AdminId, DisputeId: DisputeId)));
        BusinessResult result;
        await using (var ctx = NewContext(null, interceptor))
        {
            var handler = new ResolveDispute.Handler(
                new DisputeRepository(ctx),
                new TestUserSessionProvider(AdminId, "admin@cleansia.test"),
                NewRefundService(ctx),
                new RefundRepository(ctx),
                new CreditAccountRepository(ctx),
                new OrderEmployeePayRepository(ctx),
                new PayPeriodRepository(ctx),
                Mock.Of<ILoyaltyService>(),
                Mock.Of<INotificationProducer>(),
                Mock.Of<IAuditContext>());
            result = await handler.Handle(new ResolveDispute.Command(DisputeId, 1000m, "probe"), CancellationToken.None);

            // What UnitOfWorkPipelineBehavior does with the result.
            if (result.IsSuccess)
            {
                await ctx.CommitAsync(CancellationToken.None);
            }
            else
            {
                ctx.Rollback();
            }
        }

        Assert.True(interceptor.Ran);
        var (order, refund, dispute) = await ReadAsync(DisputeKey);
        Assert.Equal(new List<(decimal, string)> { (1000m, DisputeKey) }, _stripeRefunds);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
        Assert.Equal(1000m, dispute!.CardRefundedAmount);
        Assert.True(refund.Status == RefundStatus.Succeeded,
            $"PROBE A STATE: handler success={result.IsSuccess} error={result.Error?.Message}; stripe refunds={string.Join(",", _stripeRefunds)}; " +
            $"refund {refund.RefundKey} status={refund.Status} amount={refund.Amount}; order payment={order.PaymentStatus}; " +
            $"dispute={dispute?.Status} cardRefunded={dispute?.CardRefundedAmount?.ToString() ?? "null"}");
    }

    [Fact]
    public async Task An_Hourly_Redrive_Preserves_The_Refund_A_WindDown_Just_Paid()
    {
        await ResetAsync();
        await SeedAsync(WindDownKey, RefundReason.ServiceNotRendered, withDispute: false, cancelled: true);

        var tenants = new FixedTenantProvider(TestTenants.Default);
        var interceptor = new AtFirstRefundSum(FirstSettleCompletes(
            new RefundRequest(OrderId, 1000m, RefundReason.ServiceNotRendered, AdminId)));
        var result = await RunRedriveSweepAsync(interceptor);

        Assert.True(interceptor.Ran);
        Assert.Equal(1, result.Value!.Redriven);
        var (order, refund, _) = await ReadAsync(WindDownKey);
        Assert.Equal(new List<(decimal, string)> { (1000m, WindDownKey) }, _stripeRefunds);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
        Assert.True(refund.Status == RefundStatus.Succeeded,
            $"PROBE B STATE: job success={result.IsSuccess} considered={result.Value?.Considered} redriven={result.Value?.Redriven}; " +
            $"stripe refunds={string.Join(",", _stripeRefunds)}; refund {refund.RefundKey} status={refund.Status} amount={refund.Amount}; " +
            $"order payment={order.PaymentStatus}");
    }

    [Fact]
    public async Task A_Tracked_WindDown_Load_Preserves_The_Refund_The_Hourly_Redrive_Just_Paid()
    {
        await ResetAsync();
        await SeedAsync(WindDownKey, RefundReason.ServiceNotRendered, withDispute: false, cancelled: true);
        var interceptor = new AtFirstRefundSum(FirstSettleCompletes(
            new RefundRequest(OrderId, 1000m, RefundReason.ServiceNotRendered, AdminId)));
        await using (var ctx = NewContext(null, interceptor))
        {
            var tracked = await new RefundRepository(ctx).GetByRefundKeyAsync(WindDownKey, CancellationToken.None);
            Assert.Equal(RefundStatus.Pending, tracked!.Status);
            var result = await NewRefundService(ctx).RedriveAsync(tracked.Id, AdminId, CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error?.Message);
            await ctx.CommitAsync(CancellationToken.None);
        }
        await AssertPaidAsync(WindDownKey, 1000m);
    }

    [Fact]
    public async Task A_Card_Key_Paid_Beside_An_Earlier_Credit_Settlement_Keeps_Its_Amount()
    {
        await ResetAsync();
        await SeedAsync(WindDownKey, RefundReason.ServiceNotRendered, withDispute: false, cancelled: true,
            cardAmount: 600m, settledCredit: 400m);
        var interceptor = new AtFirstRefundSum(FirstSettleCompletes(
            new RefundRequest(OrderId, 1000m, RefundReason.ServiceNotRendered, AdminId)));
        await using (var ctx = NewContext(null, interceptor))
        {
            var claim = await new RefundRepository(ctx).GetByRefundKeyAsync(WindDownKey, CancellationToken.None);
            var result = await NewRefundService(ctx).RedriveAsync(claim!.Id, AdminId, CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(600m, result.Value!.Amount);
            await ctx.CommitAsync(CancellationToken.None);
        }
        await AssertPaidAsync(WindDownKey, 600m);
    }

    [Fact]
    public async Task A_Settle_Committed_At_The_Held_Slice_Read_Is_Not_Closed_As_Failed()
    {
        await ResetAsync();
        await SeedAsync(WindDownKey, RefundReason.ServiceNotRendered, withDispute: false, cancelled: true,
            cardAmount: 600m, settledCredit: 400m);
        var interceptor = new AtFirstRefundSum(FirstSettleCompletes(
            new RefundRequest(OrderId, 1000m, RefundReason.ServiceNotRendered, AdminId)), sumsBefore: 2);
        await using (var ctx = NewContext(null, interceptor))
        {
            var claim = await new RefundRepository(ctx).GetByRefundKeyAsync(WindDownKey, CancellationToken.None);
            var result = await NewRefundService(ctx).RedriveAsync(claim!.Id, AdminId, CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }
        Assert.True(interceptor.Ran);
        // Retry reads succeeded and earlier-pending sums first; the third SUM is held-slice
        // accounting after the committed-success reread, so it exercises the closure guard.
        Assert.Equal(3, interceptor.TriggeredAtSum);
        await AssertPaidAsync(WindDownKey, 600m);
    }

    private async Task AssertPaidAsync(string key, decimal amount)
    {
        var (order, refund, _) = await ReadAsync(key);
        Assert.Equal((key, RefundStatus.Succeeded, amount), (refund.RefundKey, refund.Status, refund.Amount));
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
        Assert.Equal(new List<(decimal, string)> { (amount, key) }, _stripeRefunds);
    }

    /// <summary>Runs the other settle to completion once, just before this context's first SUM over the refunds.</summary>
    private sealed class AtFirstRefundSum(Func<Task> settleAnother, int sumsBefore = 0) : DbCommandInterceptor
    {
        public bool Ran { get; private set; }
        public int? TriggeredAtSum { get; private set; }
        private int _sumsObserved;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await MaybeRunAsync(command);
            return result;
        }

        public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            await MaybeRunAsync(command);
            return result;
        }

        private async Task MaybeRunAsync(DbCommand command)
        {
            if (!Ran && command.CommandText.Contains("sum(", StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains("FROM \"Refunds\""))
            {
                _sumsObserved++;
                if (sumsBefore-- > 0) return;
                Ran = true;
                TriggeredAtSum = _sumsObserved;
                await settleAnother();
            }
        }
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
    private async Task<BusinessResult<RedrivePendingRefunds.Response>> RunRedriveSweepAsync(params IInterceptor[] interceptors)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<RedrivePendingRefunds>());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkPipelineBehavior<,>));
        services.AddScoped<ITenantProvider>(_ => new FixedTenantProvider(TestTenants.Default));
        services.AddScoped(sp => NewContext(sp.GetRequiredService<ITenantProvider>(), interceptors));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CleansiaDbContext>());
        services.AddScoped<IRefundRepository, RefundRepository>();
        services.AddScoped<IUserNotificationRepository, UserNotificationRepository>();
        services.AddScoped<IAdminNotifier>(sp =>
        {
            var ctx = sp.GetRequiredService<CleansiaDbContext>();
            return Mock.Of<IAdminNotifier>();
        });
        services.AddScoped<IRefundService>(sp =>
        {
            var ctx = sp.GetRequiredService<CleansiaDbContext>();
            return NewRefundService(ctx);
        });
        services.AddScoped<INotificationProducer>(sp =>
        {
            var ctx = sp.GetRequiredService<CleansiaDbContext>();
            return new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx),
                new UserRepository(ctx), NullLogger<NotificationProducer>.Instance);
        });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        return await ActivatorUtilities.CreateInstance<RedrivePendingRefunds.Handler>(scope.ServiceProvider)
            .Handle(new RedrivePendingRefunds.Command(), CancellationToken.None);
    }

}
