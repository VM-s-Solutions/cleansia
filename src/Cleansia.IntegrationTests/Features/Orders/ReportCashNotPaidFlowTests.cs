using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Receivables;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.ServiceAreas;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Service = Cleansia.Core.Domain.Services.Service;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// Owner ruling 2026-10-06 end to end on real Postgres: the assigned cleaner of a signed-in customer's cash
/// booking reports that the customer did not pay at the door. The job completes, the price becomes a debt
/// under the order's company, and the customer is refused a new booking in any company's market until it is
/// settled. The cleaner is paid the job's full reward; the booking earns no loyalty points and qualifies no
/// referral. The customer can ask for the pay link, and when they pay the cleaner after all an administrator
/// records the cash, which closes the debt in the same commit.
/// </summary>
[Collection("PostgresCollection")]
public class ReportCashNotPaidFlowTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string OrderId = "01K6D00RCASHN0TPA1D0000001";
    private const string Czk = "currency-czk-door";
    private const string OrderMarket = "country-cz-door";
    private const string OtherMarket = "country-sk-door";
    private const string ServiceId = "service-door-120";
    private const string CategoryId = "category-door";
    private const string CustomerUserId = "user-door-customer";
    private const string CustomerEmail = "door-customer@cleansia.test";
    private const string CleanerUserId = "user-door-cleaner";
    private const string CleanerEmail = "door-cleaner@cleansia.test";
    private const string CleanerEmployeeId = "employee-door-cleaner";
    private const string AdminUserId = "user-door-admin";
    private const decimal Price = 1500m;

    private TestUserSessionProvider _session = new(new TestClaimsPrincipalUser());
    private string _tenant = TestTenants.Default;

    [Fact]
    public async Task The_Report_Completes_The_Job_Opens_The_Debt_Under_The_Orders_Company_And_Refuses_The_Customer_Elsewhere()
    {
        await TestMethod(
            setup: Setup,
            arrange: context => SeedAsync(context, orderCompany: TestTenants.Second),
            act: async provider =>
            {
                var report = await SendAs(provider, Cleaner(), TestTenants.Second, new ReportCashNotPaid.Command(OrderId));
                var booking = await SendAs(provider, Customer(), TestTenants.Default, CardBookingIn(OtherMarket));
                return (report, booking);
            },
            assert: async (CleansiaDbContext context,
                (BusinessResult<ReportCashNotPaid.Response> Report, BusinessResult<CreateOrder.Response> Booking) outcome) =>
            {
                Assert.True(outcome.Report.IsSuccess, outcome.Report.Error?.Message);
                Assert.Equal(Price, outcome.Report.Value.AmountOwed);

                var order = await context.Orders.IgnoreQueryFilters().Include(o => o.OrderStatusHistory).SingleAsync(o => o.Id == OrderId);
                Assert.Equal(OrderStatus.Completed, order.CurrentStatus);
                Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
                Assert.NotNull(order.CompletedAt);

                var debt = await context.Receivables.IgnoreQueryFilters().SingleAsync(r => r.OrderId == OrderId);
                Assert.Equal(
                    (ReceivableKind.UnpaidCash, ReceivableStatus.Open, Price, CustomerUserId, TestTenants.Second),
                    (debt.Kind, debt.Status, debt.Amount, debt.UserId, debt.TenantId));

                var outbox = await context.OutboxMessages.IgnoreQueryFilters().ToListAsync();
                Assert.Single(outbox, m => m.QueueName == QueueNames.CalculateOrderPay
                    && m.MessageKey == MessageKeys.Pay(OrderId, CleanerEmployeeId) && m.TenantId == TestTenants.Second);
                Assert.Single(outbox, m => m.QueueName == QueueNames.SendEmail
                    && m.MessageKey == MessageKeys.OrderCashNotPaidEmail(debt.Id));
                Assert.DoesNotContain(outbox, m => m.QueueName == QueueNames.GenerateReceipt);
                Assert.Empty(await context.CashLedgerEntries.IgnoreQueryFilters().ToListAsync());

                Assert.True(outcome.Booking.IsFailure);
                var refusal = Assert.Single(Assert.IsAssignableFrom<IValidationResult>(outcome.Booking).Errors);
                Assert.Equal(BusinessErrorMessage.OrderUnpaidReceivable, refusal.Message);
                Assert.Equal(1, await context.Orders.IgnoreQueryFilters().CountAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Cleaner_Is_Paid_In_Full_No_Points_Or_Referral_Come_Of_It_And_Cash_Recorded_Later_Closes_The_Debt()
    {
        await TestMethod(
            setup: Setup,
            arrange: context => SeedAsync(context, orderCompany: TestTenants.Default),
            act: async provider =>
            {
                var report = await SendAs(provider, Cleaner(), TestTenants.Default, new ReportCashNotPaid.Command(OrderId));
                var pay = await SendAs(provider, Cleaner(), TestTenants.Default, new CalculateOrderPay.Command(OrderId, CleanerEmployeeId));

                string receivableId;
                using (var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope())
                {
                    receivableId = (await scope.ServiceProvider.GetRequiredService<CleansiaDbContext>().Receivables
                        .IgnoreQueryFilters().SingleAsync(r => r.OrderId == OrderId)).Id;
                }

                var payLink = await SendAs(provider, Customer(), TestTenants.Default, new CreateReceivablePayLink.Command(receivableId));
                var cash = await SendAs(provider, Admin(), TestTenants.Default,
                    new AdminRecordCashReceived.Command(OrderId, CleanerEmployeeId, DateTime.UtcNow.AddMinutes(-10), Price));
                return (report, pay, payLink, cash);
            },
            assert: async (CleansiaDbContext context,
                (BusinessResult<ReportCashNotPaid.Response> Report, BusinessResult<CalculateOrderPay.Response> Pay,
                    BusinessResult<CreateReceivablePayLink.Response> PayLink, BusinessResult<AdminRecordCashReceived.Response> Cash) outcome) =>
            {
                Assert.True(outcome.Report.IsSuccess, outcome.Report.Error?.Message);
                Assert.True(outcome.Pay.IsSuccess, outcome.Pay.Error?.Message);
                Assert.True(outcome.PayLink.IsSuccess, outcome.PayLink.Error?.Message);
                Assert.True(outcome.Cash.IsSuccess, outcome.Cash.Error?.Message);

                var pay = await context.Set<OrderEmployeePay>().IgnoreQueryFilters().SingleAsync(p => p.OrderId == OrderId);
                Assert.Equal((PayLineType.Job, 1000m), (pay.LineType, pay.TotalPay));

                Assert.Empty(await context.LoyaltyTransactions.IgnoreQueryFilters().ToListAsync());
                var referral = await context.Referrals.IgnoreQueryFilters().SingleAsync();
                Assert.Equal(ReferralStatus.Accepted, referral.Status);

                var debt = await context.Receivables.IgnoreQueryFilters().SingleAsync(r => r.OrderId == OrderId);
                Assert.Equal(
                    (ReceivableStatus.WrittenOff, AdminRecordCashReceived.PaidInCashNote, AdminUserId, "cs_door"),
                    (debt.Status, debt.WriteOffNote, debt.WrittenOffByUserId, debt.PayLinkSessionId));

                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
                Assert.Equal((PaymentStatus.Paid, (string?)CleanerEmployeeId, (decimal?)Price),
                    (order.PaymentStatus, order.CollectedByEmployeeId, order.CashCollectedAmount));
                Assert.Single(await context.CashLedgerEntries.IgnoreQueryFilters()
                    .Where(e => e.EmployeeId == CleanerEmployeeId && e.OrderId == OrderId).ToListAsync());
                Assert.Single(await context.OutboxMessages.IgnoreQueryFilters()
                    .Where(m => m.QueueName == QueueNames.GenerateReceipt && m.MessageKey == MessageKeys.Receipt(OrderId)).ToListAsync());
            },
            transactional: false);
    }

    private Task Setup(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => _session));
        services.Replace(ServiceDescriptor.Scoped<ITenantProvider>(sp =>
        {
            var tenantProvider = new TenantProvider(sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>());
            tenantProvider.SetTenantOverride(_tenant);
            return tenantProvider;
        }));
        services.Replace(ServiceDescriptor.Singleton<IOrderChannelProvider>(_ => new OrderChannelProvider(OrderChannel.Mobile)));
        services.Replace(ServiceDescriptor.Scoped<IAddressGeocoder, NoopAddressGeocoder>());
        services.Replace(ServiceDescriptor.Scoped<IEmailService>(_ => Mock.Of<IEmailService>()));

        var stripe = new Mock<IStripeClient>();
        stripe
            .Setup(s => s.CreateReceivableCheckoutSessionAsync(
                It.IsAny<string>(), It.IsAny<string?>(), OrderId, It.IsAny<string>(), Price, "CZK", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutSessionResult("cs_door", "https://checkout.stripe.test/cs_door"));
        services.Replace(ServiceDescriptor.Transient<IStripeClient>(_ => stripe.Object));
        var stripeConfig = new Mock<IStripeConfig>();
        stripeConfig.SetupGet(c => c.Enabled).Returns(true);
        services.Replace(ServiceDescriptor.Singleton(stripeConfig.Object));
        return Task.CompletedTask;
    }

    private async Task<BusinessResult<TResponse>> SendAs<TResponse>(
        IServiceProvider provider, TestUserSessionProvider session, string tenant, IRequest<BusinessResult<TResponse>> request)
    {
        _session = session;
        _tenant = tenant;
        using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    private static TestUserSessionProvider Cleaner() => new(
        CleanerUserId, CleanerEmail,
        [
            new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()),
            new Claim(TestUserSessionProvider.EmployeeIdClaimType, CleanerEmployeeId),
        ]);

    private static TestUserSessionProvider Customer() => new(
        CustomerUserId, CustomerEmail, [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())]);

    private static TestUserSessionProvider Admin() => new(
        AdminUserId, "door-admin@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())]);

    private static CreateOrder.Command CardBookingIn(string countryId) => new(
        CustomerName: "Door Customer",
        CustomerEmail: CustomerEmail,
        CustomerPhone: "+420777222111",
        CustomerAddress: new AddressDto("Druha 5", "Bratislava", "81101", countryId, null),
        SavedAddressId: null,
        SelectedPackageIds: [],
        SelectedServiceIds: [ServiceId],
        Rooms: 2,
        Bathrooms: 1,
        Extras: new Dictionary<string, bool>(),
        CleaningDate: DateTime.UtcNow.Date.AddDays(3).AddHours(9),
        PaymentType: PaymentType.Card,
        CurrencyId: null,
        TotalPrice: 900m,
        TermsAccepted: true,
        EarlyPerformanceRequested: true);

    /// <summary>
    /// Two markets in one currency: the order's, run by <paramref name="orderCompany"/>, and another run by
    /// the default company. The customer's account is the default company's, was referred by a friend, and
    /// their currency earns points and pays referral credit, so a completion that granted either would show.
    /// The cleaner works for the order's company, is on the order's one seat under a contract whose reward
    /// froze at 840 + 280 capped at 1 000, has uploaded the after photo and has started the job.
    /// </summary>
    private static async Task SeedAsync(CleansiaDbContext context, string orderCompany)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var (contract, _) = TestLegalDocuments.Add(context);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = Czk;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        currency.SetLoyaltyPointsDivisor(10m);
        currency.SetReferralCredit(150m);
        context.Currencies.Add(currency);

        foreach (var (countryId, isoCode, operatorId) in new[] { (OrderMarket, "CZ", orderCompany), (OtherMarket, "SK", TestTenants.Default) })
        {
            var country = Country.Create(isoCode == "CZ" ? "Czechia" : "Slovakia", isoCode, isoCode, isServiced: true);
            country.Id = countryId;
            context.Countries.Add(country);
            context.CountryConfigurations.Add(CountryConfiguration.Create(countryId, "CZK", "cs", 0.21m).AssignOperator(operatorId));
            context.Add(ServiceCity.Create(countryId, isoCode == "CZ" ? "Praha" : "Bratislava"));
        }

        var category = ServiceCategory.Create("door", "Door", "Category under test");
        category.Id = CategoryId;
        context.Add(category);
        var service = Service.Create(CategoryId, ServiceId, "Under test", 120);
        service.Id = ServiceId;
        context.Add(service);
        context.EmployeePayConfigs.Add(EmployeePayConfig.CreateForService(ServiceId, 600m, Czk));
        context.ServicePrices.Add(ServicePrice.Create(ServiceId, Czk, 900m, 0m));

        var customer = User.CreateWithPassword(
            CustomerEmail, TestConstants.TestUserSession.TestUserPassword, "Door", "Customer", UserProfile.Customer);
        customer.Id = CustomerUserId;
        customer.ConfirmEmail();
        context.Users.Add(customer);
        var referrer = User.CreateWithPassword(
            "door-referrer@cleansia.test", TestConstants.TestUserSession.TestUserPassword, "Door", "Friend", UserProfile.Customer);
        context.Users.Add(referrer);
        var code = ReferralCode.Generate(referrer.Id, "DOORFR", "system");
        context.ReferralCodes.Add(code);
        context.Referrals.Add(Referral.CreateAccepted(referrer.Id, customer.Id, code.Id, "system"));
        BaseIntegrationTest.StampUnstampedAdded(context, TestTenants.Default);

        var cleanerUser = User.CreateWithPassword(
            CleanerEmail, TestConstants.TestUserSession.TestUserPassword, "Petra", "Svobodova", UserProfile.Employee);
        cleanerUser.Id = CleanerUserId;
        cleanerUser.ConfirmEmail();
        context.Users.Add(cleanerUser);
        var cleaner = Employee.CreateWithUser(cleanerUser);
        cleaner.Id = CleanerEmployeeId;
        cleaner.Approve(approvedByUserId: "admin-door");
        cleaner.AssignWorkCountry(OrderMarket);
        cleaner.UpdateAddress(Address.Create("Korunni 9", "Praha", "12000", OrderMarket));
        context.Employees.Add(cleaner);
        context.Set<PayPeriod>().Add(PayPeriod.CreateBiWeekly(DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-3))));

        var order = Order.Create(
            customerName: "Door Customer",
            customerEmail: CustomerEmail,
            customerPhone: "+420777222111",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", OrderMarket),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(-2),
            paymentType: PaymentType.Cash,
            totalPrice: Price,
            currencyId: Czk,
            paymentStatus: PaymentStatus.Pending,
            userId: CustomerUserId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.UpdateEstimatedTime(120).CalculateRequiredEmployees(spareSeats: 0);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        var seat = OrderEmployee.Create(order, cleaner);
        seat.FreezeJobPay((840m, 280m, 500m, 1000m));
        order.AddAssignedEmployee(seat);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.InProgress, order));
        context.Orders.Add(order);
        context.WorkContractAcceptances.Add(WorkContractAcceptance.Create(
            OrderId, seat.Id, CleanerEmployeeId, contract.TextFor("en")!, contract.Version, "cleansia.mobile",
            ipAddress: null, deviceLabel: null, deviceId: null, factsJson: "{}"));
        context.Add(OrderPhoto.Create(
            orderId: OrderId,
            photoType: PhotoType.After,
            blobUrl: "https://account.blob.core.windows.net/order-photos/door/after.jpg",
            fileName: "after.jpg",
            originalFileName: "after.jpg",
            fileSizeBytes: 2048,
            contentType: "image/jpeg",
            capturedByEmployeeId: CleanerEmployeeId));
        BaseIntegrationTest.StampUnstampedAdded(context, orderCompany);

        await context.CommitAsync(CancellationToken.None);
    }

    private sealed class NoopAddressGeocoder : IAddressGeocoder
    {
        public Task PopulateCoordinatesAsync(Address address, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
