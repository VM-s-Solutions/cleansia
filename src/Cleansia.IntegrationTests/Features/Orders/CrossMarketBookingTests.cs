using Cleansia.Core.AppServices.Features.Auditing;
using Cleansia.Core.AppServices.Features.Auditing.Filters;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Orders;

public partial class CreateOrderCallerCurrencyTests
{
    private const string CrossOrderId = "cross-market-order";
    private const string SavedSlovakAddressId = "saved-cross-market";

    private static void AsAccount(IServiceProvider provider)
        => provider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Default);

    [Fact]
    public async Task CrossMarket_Booking_Reads_Cancel_And_Account_Audit_Timeline_Work_Across_Operators()
    {
        await TestMethod(setup: ConfigureCustomerSession, arrange: SeedWithSlovakiaOperatedBySecondCompanyAsync,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var created = await mediator.Send(BuildCommand(Slovakia, null, 60m) with { PaymentType = PaymentType.Cash });
                Assert.True(created.IsSuccess, created.Error?.Message);
                AsAccount(provider);
                var page = await mediator.Send(new GetCustomerOrders.Request());
                var listed = Assert.Single(page.Data);
                Assert.Equal(created.Value.Id, listed.Id);
                Assert.Equal(Eur, listed.CurrencyId);
                Assert.Equal(Slovakia, listed.CountryId);
                var detail = await mediator.Send(new GetOrderDetails.Query(created.Value.Id));
                Assert.True(detail.IsSuccess, detail.Error?.Message);
                Assert.Null(detail.Value.CustomerCompany);
                Assert.Equal(Slovakia, detail.Value.CountryId);
                var preview = await mediator.Send(new GetCancellationFeePreview.Query(created.Value.Id));
                Assert.True(preview.IsSuccess, preview.Error?.Message);
                var cancelled = await mediator.Send(new CancelOrder.Command(created.Value.Id, null));
                Assert.True(cancelled.IsSuccess, cancelled.Error?.Message);
                AsAccount(provider);
                var timeline = await mediator.Send(new GetActionTimeline.Request { UserId = CustomerUserId });
                Assert.Contains(timeline.Data, row => row.Action == "customer.order.cancel" && row.ResourceId == created.Value.Id);
                var audit = await mediator.Send(new GetPagedCustomerActionAudits.Request
                {
                    Filter = new CustomerActionAuditFilter(CustomerUserId, null, null, null, null, null, null, null)
                });
                Assert.Contains(audit.Data, row => row.Action == "customer.order.cancel" && row.ResourceId == created.Value.Id);
                return created.Value.Id;
            },
            assert: async (context, id) =>
            {
                Assert.Equal(TestTenants.Second, (await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == id)).TenantId);
                var audit = await context.CustomerActionAudits.IgnoreQueryFilters().SingleAsync(a => a.Action == "customer.order.cancel");
                Assert.Equal(TestTenants.Second, audit.TenantId);
                Assert.Equal(CustomerUserId, audit.UserId);
                Assert.All(await context.Set<OrderStatusTrack>().IgnoreQueryFilters().Where(t => t.OrderId == id).ToListAsync(),
                    row => Assert.Equal(TestTenants.Second, row.TenantId));
            }, transactional: false);
    }

    private static async Task SeedCrossOrderAsync(CleansiaDbContext context)
    {
        await SeedWithSlovakiaOperatedBySecondCompanyAsync(context);
        var address = Address.Create("Testovaci 12", "Bratislava", "11000", Slovakia);
        address.TenantId = TestTenants.Second;
        var order = Order.Create("Caller Customer", CustomerEmail, "+420777111555", address, 2, 1,
            DateTime.UtcNow.AddHours(-2), PaymentType.Cash, 60m, Eur, PaymentStatus.Pending, userId: CustomerUserId);
        order.Id = CrossOrderId;
        order.TenantId = TestTenants.Second;
        order.SetMaxEmployees(1);
        var status = OrderStatusTrack.Create(OrderStatus.New, order);
        status.TenantId = TestTenants.Second;
        order.AddOrderStatus(status);
        context.Orders.Add(order);
        var eur = await context.Currencies.SingleAsync(c => c.Id == Eur);
        eur.SetLoyaltyPointsDivisor(1m);
        eur.SetNoShowCredit(10m);
        await context.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CrossMarket_Dispute_And_Its_Audit_Follow_The_Order_And_Remain_Readable_By_The_Customer()
    {
        await TestMethod(setup: ConfigureCustomerSession, arrange: SeedCrossOrderAsync,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var created = await mediator.Send(new CreateDispute.Command(CrossOrderId, DisputeReason.QualityIssue, "The bathroom cleaning was not completed."));
                Assert.True(created.IsSuccess, created.Error?.Message);
                AsAccount(provider);
                var detail = await mediator.Send(new GetDisputeDetails.Query(created.Value.DisputeId));
                Assert.True(detail.IsSuccess, detail.Error?.Message);
                return created.Value.DisputeId;
            },
            assert: async (context, id) =>
            {
                Assert.Equal(TestTenants.Second, (await context.Disputes.IgnoreQueryFilters().SingleAsync(d => d.Id == id)).TenantId);
                Assert.Equal(TestTenants.Second, (await context.CustomerActionAudits.IgnoreQueryFilters().SingleAsync(a => a.ResourceId == id)).TenantId);
            }, transactional: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrossMarket_Completion_Uses_The_Single_Account_Loyalty_Row(bool accountExists)
    {
        await TestMethod(setup: ConfigureCustomerSession,
            arrange: async context =>
            {
                await SeedCrossOrderAsync(context);
                if (accountExists) context.LoyaltyAccounts.Add(LoyaltyAccount.Create(CustomerUserId));
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                provider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Second);
                var handler = ActivatorUtilities.CreateInstance<CompleteOrder.Handler>(provider);
                var result = await handler.Handle(new CompleteOrder.Command(CrossOrderId, 60), CancellationToken.None);
                Assert.True(result.IsSuccess, result.Error?.Message);
                await provider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);
                await provider.GetRequiredService<ILoyaltyService>().GrantForCompletedOrderAsync(CrossOrderId, CancellationToken.None);
                await provider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);
                return result;
            },
            assert: async (context, _) =>
            {
                var account = Assert.Single(await context.LoyaltyAccounts.IgnoreQueryFilters().Where(a => a.UserId == CustomerUserId).ToListAsync());
                Assert.Equal(TestTenants.Default, account.TenantId);
                Assert.Equal(60, account.LifetimePoints);
                var earned = Assert.Single(await context.LoyaltyTransactions.IgnoreQueryFilters().Where(t => t.OrderId == CrossOrderId).ToListAsync());
                Assert.Equal(TestTenants.Default, earned.TenantId);
            }, transactional: false);
    }

    [Fact]
    public async Task CrossMarket_Unfilled_Order_Credits_The_Account_Company_In_The_Order_Currency()
    {
        await TestMethod(setup: ConfigureCustomerSession, arrange: SeedCrossOrderAsync,
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new CancelUnfilledOrders.Command()),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Equal(1, result.Value.CreditedCount);
                var account = Assert.Single(await context.CreditAccounts.IgnoreQueryFilters().Where(a => a.UserId == CustomerUserId).ToListAsync());
                Assert.Equal(TestTenants.Default, account.TenantId);
                Assert.Equal(Eur, account.CurrencyId);
                Assert.Equal(10m, account.Balance);
            }, transactional: false);
    }

    private static async Task SeedCrossMembershipAsync(CleansiaDbContext context)
    {
        await SeedWithSlovakiaOperatedBySecondCompanyAsync(context);
        var plan = MembershipPlan.Create("PLUS", "Plus", 0m, 4, true);
        context.MembershipPlans.Add(plan);
        context.UserMemberships.Add(UserMembership.Create(CustomerUserId, plan.Id, Czk, "sub_cross_market", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMonths(1)));
        var address = Address.Create("Testovaci 12", "Bratislava", "11000", Slovakia);
        context.Addresses.Add(address);
        var saved = SavedAddress.Create(CustomerUserId, address.Id, "Slovak home", false);
        saved.Id = SavedSlovakAddressId;
        context.SavedAddresses.Add(saved);
        var czechAddress = Address.Create("Home 1", "Praha", "11000", Czechia);
        context.Addresses.Add(czechAddress);
        var czechSaved = SavedAddress.Create(CustomerUserId, czechAddress.Id, "Czech home", false);
        czechSaved.Id = "saved-czech-home";
        context.SavedAddresses.Add(czechSaved);
        await context.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CrossMarket_Recurring_Template_And_Occurrences_Use_The_Address_Operator_While_Entitlement_Is_Account_Owned()
    {
        await TestMethod(setup: ConfigureCustomerSession, arrange: SeedCrossMembershipAsync,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var starts = DateTime.UtcNow.AddDays(2).Date;
                var created = await mediator.Send(new CreateRecurringBooking.Command((int)RecurrenceFrequency.Weekly,
                    (int)starts.DayOfWeek, "10:00", 2, 1, SavedSlovakAddressId, [ServiceId], [PackageId], (int)PaymentType.Cash, starts));
                Assert.True(created.IsSuccess, created.Error?.Message);
                var own = await provider.GetRequiredService<IRecurringBookingTemplateRepository>().GetByIdForOwnerAsync(created.Value.Id, CustomerUserId, CancellationToken.None);
                Assert.NotNull(own);
                Assert.Equal(TestTenants.Second, own.TenantId);
                var update = new UpdateRecurringBooking.Command(own.Id, (int)RecurrenceFrequency.Weekly,
                    (int)starts.DayOfWeek, "10:00", 2, 1, "saved-czech-home", [ServiceId], [PackageId], (int)PaymentType.Cash, starts);
                Assert.True((await mediator.Send(update)).IsSuccess);
                Assert.Equal(TestTenants.Default, own.TenantId);
                Assert.True((await mediator.Send(update with { SavedAddressId = SavedSlovakAddressId })).IsSuccess);
                Assert.Equal(TestTenants.Second, own.TenantId);
                Assert.Null(await provider.GetRequiredService<IRecurringBookingTemplateRepository>()
                    .GetByIdForOwnerAsync(own.Id, "another-customer", CancellationToken.None));
                Assert.True((await mediator.Send(new SetRecurringBookingActive.Command(own.Id, false))).IsSuccess);
                Assert.True((await mediator.Send(new SetRecurringBookingActive.Command(own.Id, true))).IsSuccess);
                var materializer = ActivatorUtilities.CreateInstance<MaterializeRecurringBookingTemplate.Handler>(provider);
                var result = await materializer.Handle(new MaterializeRecurringBookingTemplate.Command(own.Id, DateTime.UtcNow, 7), CancellationToken.None);
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.True(result.Value.OrdersCreated > 0);
                return own.Id;
            },
            assert: async (context, id) =>
            {
                var orders = await context.Orders.IgnoreQueryFilters().Where(o => o.RecurringTemplateId == id).ToListAsync();
                Assert.NotEmpty(orders);
                Assert.All(orders, o => Assert.Equal(TestTenants.Second, o.TenantId));
                Assert.All(orders, o => Assert.Equal(Eur, o.CurrencyId));
                var addressIds = orders.Select(o => o.CustomerAddressId).ToList();
                Assert.All(await context.Addresses.IgnoreQueryFilters().Where(a => addressIds.Contains(a.Id)).ToListAsync(),
                    address => Assert.Equal(TestTenants.Second, address.TenantId));
                Assert.Equal(TestTenants.Default, (await context.SavedAddresses.IgnoreQueryFilters().SingleAsync(a => a.Id == SavedSlovakAddressId)).TenantId);
                Assert.Equal(TestTenants.Default, (await context.UserMemberships.IgnoreQueryFilters().SingleAsync()).TenantId);
            }, transactional: false);
    }

    [Fact]
    public async Task CrossMarket_Owned_Order_Refusal_Uses_The_Order_Operator_And_Other_Customers_Cannot_Read_It()
    {
        await TestMethod(setup: ConfigureCustomerSession,
            arrange: async context =>
            {
                await SeedCrossOrderAsync(context);
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == CrossOrderId);
                var status = OrderStatusTrack.Create(OrderStatus.InProgress, order);
                status.TenantId = TestTenants.Second;
                order.AddOrderStatus(status);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var orders = provider.GetRequiredService<IOrderRepository>();
                Assert.Null(await orders.GetByIdForOwnerAsync(CrossOrderId, "another-customer", CancellationToken.None));
                Assert.Equal(0, await orders.GetCountForOwnerAsync("another-customer", null, CancellationToken.None));
                var result = await provider.GetRequiredService<IMediator>().Send(new CancelOrder.Command(CrossOrderId, null));
                Assert.True(result.IsFailure);
                return result;
            },
            assert: async (context, _) =>
            {
                var row = await context.CustomerActionAudits.IgnoreQueryFilters().SingleAsync(a => a.Action == "customer.order.cancel");
                Assert.False(row.Success);
                Assert.Equal(TestTenants.Second, row.TenantId);
                Assert.Equal(CustomerUserId, row.UserId);
            }, transactional: false);
    }

    [Fact]
    public async Task CrossMarket_Express_Reservation_Is_Stamped_With_The_Membership_Company_And_Readable_From_Either_Operator()
    {
        await TestMethod(setup: ConfigureCustomerSession, arrange: SeedCrossMembershipAsync,
            act: async provider =>
            {
                var membership = await provider.GetRequiredService<IUserMembershipRepository>().GetEntitledForUserAsync(CustomerUserId, CancellationToken.None);
                provider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Second);
                var usages = provider.GetRequiredService<IMembershipBenefitUsageRepository>();
                var slot = await usages.TryReserveSlotAsync(CustomerUserId, MembershipBenefitKind.ExpressUpgrade, "2026-09", membership!.Id, 1, DateTime.UtcNow, CancellationToken.None);
                Assert.NotNull(slot);
                Assert.Equal(TestTenants.Default, slot.TenantId);
                AsAccount(provider);
                Assert.Equal(1, await usages.CountLiveInPeriodAsync(CustomerUserId, MembershipBenefitKind.ExpressUpgrade, "2026-09", CancellationToken.None));
                return slot.Id;
            },
            assert: async (context, id) => Assert.Equal(TestTenants.Default,
                (await context.MembershipBenefitUsages.IgnoreQueryFilters().SingleAsync(u => u.Id == id)).TenantId), transactional: false);
    }
}
