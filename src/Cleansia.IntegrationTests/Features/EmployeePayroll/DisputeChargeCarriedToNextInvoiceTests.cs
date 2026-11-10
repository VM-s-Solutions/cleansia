using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// A dispute charge on a job that is already invoiced becomes a line of its own in the open period, and the
/// cleaner's next invoice deducts it whole, or it waits whole for a later one (owner ruling 2026-11-09). Over
/// the migration-built Postgres, each step in a scope of its own, as separate requests and runs are.
/// </summary>
[Collection("PostgresCollection")]
public class DisputeChargeCarriedToNextInvoiceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-carried";
    private const string CurrencyId = "cur-czk-carried";
    private const string Reason = "Bathroom left uncleaned";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly Mock<IEmailService> _email = new();
    private string _employeeId = string.Empty;
    private string _invoicedOrderId = string.Empty;
    private string _disputeId = string.Empty;
    private string _openPeriodId = string.Empty;

    /// <summary>
    /// Job J1 was invoiced at 600 in an earlier period. The dispute on it is resolved with a 150 charge while
    /// the cleaner has 800 of pay in the open period; that period's close invoices 800 less 150, 650. A cancel
    /// frees both rows and the corrected invoice takes the charge again.
    /// </summary>
    [Fact]
    public async Task A_Charge_On_An_Invoiced_Job_Is_Carried_And_Deducted_Whole_By_The_Next_Invoice()
    {
        await TestMethod(
            setup: RegisterBatch,
            arrange: async context =>
            {
                await SeedInvoicedJobAsync(context);
                var open = PayPeriod.Create(Today.AddDays(-14), Today.AddDays(-2));
                context.Add(open);
                var order = NewOrder("carried-next@cleansia.test");
                context.Add(order);
                await context.CommitAsync(CancellationToken.None);
                context.Add(OrderEmployeePay.Create(order.Id, _employeeId, open.Id, CurrencyId, basePay: 800m, totalPay: 800m));
                await context.CommitAsync(CancellationToken.None);
                _openPeriodId = open.Id;
            },
            act: async provider =>
            {
                var resolved = await SendAsync(provider, ResolveWithCharge(150m));
                Assert.True(resolved.IsSuccess, resolved.Error?.Message);

                using (var scope = provider.CreateScope())
                {
                    var pays = scope.ServiceProvider.GetRequiredService<IOrderEmployeePayRepository>();
                    var job = await pays.GetByOrderAndEmployeeAsync(_invoicedOrderId, _employeeId, CancellationToken.None);
                    Assert.Equal((PayLineType.Job, 600m), (job!.LineType, job.TotalPay));
                    var totals = await pays.GetTotalPayByOrderIdsAsync([_invoicedOrderId], _employeeId, CancellationToken.None);
                    Assert.Equal(600m, Assert.Single(totals).Value);
                }

                using (var scope = provider.CreateScope())
                {
                    await scope.ServiceProvider.GetRequiredService<IPayPeriodBackgroundService>()
                        .CloseExpiredPeriodsAndOpenNewAsync(CancellationToken.None);
                }

                string closedInvoiceId;
                using (var scope = provider.CreateScope())
                {
                    closedInvoiceId = await scope.ServiceProvider.GetRequiredService<CleansiaDbContext>().EmployeeInvoices
                        .Where(i => i.PayPeriodId == _openPeriodId)
                        .Select(i => i.Id)
                        .SingleAsync();
                }

                var cancelled = await SendAsync(provider, new CancelInvoice.Command(closedInvoiceId, "Wrong bank account"));
                Assert.True(cancelled.IsSuccess, cancelled.Error?.Message);
                var reissued = await SendAsync(provider, new GenerateInvoice.Command(_employeeId, _openPeriodId));
                Assert.True(reissued.IsSuccess, reissued.Error?.Message);
                return (Closed: closedInvoiceId, Corrected: Assert.Single(reissued.Value!.InvoiceIds));
            },
            assert: async (CleansiaDbContext context, (string Closed, string Corrected) ids) =>
            {
                var invoices = await context.EmployeeInvoices.IgnoreQueryFilters()
                    .Where(i => i.PayPeriodId == _openPeriodId)
                    .ToListAsync();
                Assert.All(invoices, i => Assert.Equal(
                    (2, 800m, 150m, 650m), (i.TotalOrders, i.SubTotal, i.DeductionAmount, i.TotalAmount)));
                Assert.Equal(EmployeeInvoiceStatus.Cancelled, invoices.Single(i => i.Id == ids.Closed).Status);

                var rows = await context.OrderEmployeePays.IgnoreQueryFilters()
                    .Where(p => p.OrderId == _invoicedOrderId)
                    .ToListAsync();
                var job = rows.Single(p => p.LineType == PayLineType.Job);
                Assert.Equal((600m, 0m), (job.TotalPay, job.DeductionPay));
                Assert.NotEqual(_openPeriodId, job.PayPeriodId);
                var charge = rows.Single(p => p.LineType == PayLineType.DisputeCharge);
                Assert.Equal((150m, -150m, _openPeriodId, ids.Corrected),
                    (charge.DeductionPay, charge.TotalPay, charge.PayPeriodId, charge.EmployeeInvoiceId));
                Assert.Equal((_disputeId, Reason, CurrencyId), (charge.DeductionDisputeId, charge.DeductionReason, charge.CurrencyId));
            },
            transactional: false);
    }

    /// <summary>
    /// 100 of pay and a 150 charge: the invoice is the 100, and the charge moves to the open period whole, so
    /// the closed period ends fully invoiced.
    /// </summary>
    [Fact]
    public async Task A_Charge_The_Next_Invoice_Cannot_Cover_Waits_Whole_In_The_Open_Period()
    {
        string closedId = default!;

        await TestMethod(
            setup: RegisterBatch,
            arrange: async context =>
            {
                var job = await SeedInvoicedJobAsync(context);
                var closed = PayPeriod.Create(Today.AddDays(-26), Today.AddDays(-13)).Close("System");
                var open = PayPeriod.Create(Today.AddDays(-12), Today.AddDays(1));
                context.AddRange(closed, open);
                var order = NewOrder("carried-small@cleansia.test");
                context.Add(order);
                await context.CommitAsync(CancellationToken.None);
                context.AddRange(
                    OrderEmployeePay.Create(order.Id, _employeeId, closed.Id, CurrencyId, basePay: 100m, totalPay: 100m),
                    job.CarryDisputeCharge(closed.Id, _disputeId, 150m, Reason));
                await context.CommitAsync(CancellationToken.None);
                closedId = closed.Id;
                _openPeriodId = open.Id;
            },
            act: async provider =>
            {
                var result = await SendAsync(provider, new GenerateInvoice.Command(_employeeId, closedId));
                Assert.True(result.IsSuccess, result.Error?.Message);
                return Assert.Single(result.Value!.InvoiceIds);
            },
            assert: async (context, invoiceId) =>
            {
                var invoice = await context.EmployeeInvoices.IgnoreQueryFilters().SingleAsync(i => i.Id == invoiceId);
                Assert.Equal((1, 100m, 0m, 100m), (invoice.TotalOrders, invoice.SubTotal, invoice.DeductionAmount, invoice.TotalAmount));

                var charge = await context.OrderEmployeePays.IgnoreQueryFilters()
                    .SingleAsync(p => p.LineType == PayLineType.DisputeCharge);
                Assert.Equal((_openPeriodId, (string?)null, -150m), (charge.PayPeriodId, charge.EmployeeInvoiceId, charge.TotalPay));
                Assert.False(await context.OrderEmployeePays.IgnoreQueryFilters()
                    .AnyAsync(p => p.PayPeriodId == closedId && p.EmployeeInvoiceId == null));
            },
            transactional: false);
    }

    /// <summary>
    /// A double click runs two resolves of one dispute past every check before either commits. The second
    /// commit is refused by the one-charge-per-job index, so the cleaner is charged once; a resolve after that
    /// is refused, and so is a charge from another dispute on the same job.
    /// </summary>
    [Fact]
    public async Task A_Job_Carries_One_Charge_However_Often_It_Is_Resolved()
    {
        await TestMethod(
            setup: RegisterBatch,
            arrange: async context =>
            {
                await SeedInvoicedJobAsync(context);
                context.Add(PayPeriod.Create(Today.AddDays(-5), Today.AddDays(8)));
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                using var first = provider.CreateScope();
                using var second = provider.CreateScope();
                foreach (var scope in new[] { first, second })
                {
                    var handled = await scope.ServiceProvider
                        .GetRequiredService<IRequestHandler<ResolveDispute.Command, BusinessResult>>()
                        .Handle(ResolveWithCharge(150m), CancellationToken.None);
                    Assert.True(handled.IsSuccess, handled.Error?.Message);
                }

                await first.ServiceProvider.GetRequiredService<CleansiaDbContext>().CommitAsync(CancellationToken.None);
                var refused = await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
                    second.ServiceProvider.GetRequiredService<CleansiaDbContext>().CommitAsync(CancellationToken.None));
                Assert.True(DbConstraintViolation.IsUniqueViolation(refused));

                var again = await SendAsync(provider, ResolveWithCharge(150m));

                string otherDisputeId;
                using (var scope = provider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                    var other = new Dispute(_invoicedOrderId, null, DisputeReason.QualityIssue, "Window left dirty", "customer");
                    context.Add(other);
                    await context.CommitAsync(CancellationToken.None);
                    otherDisputeId = other.Id;
                }

                var fromOther = await SendAsync(provider, new ResolveDispute.Command(otherDisputeId, null, "at fault",
                    new ResolveDispute.CleanerCharge(_employeeId, 50m, "Window left dirty")));
                return (Again: again, FromOther: fromOther);
            },
            assert: async (CleansiaDbContext context, (BusinessResult Again, BusinessResult FromOther) results) =>
            {
                Assert.Equal(BusinessErrorMessage.DisputeAlreadyResolved, results.Again.Error?.Message);
                Assert.Equal(BusinessErrorMessage.DisputeCleanerChargeNotChargeable, results.FromOther.Error?.Message);

                var charge = await context.OrderEmployeePays.IgnoreQueryFilters()
                    .SingleAsync(p => p.LineType == PayLineType.DisputeCharge);
                Assert.Equal((150m, _disputeId), (charge.DeductionPay, charge.DeductionDisputeId));
            },
            transactional: false);
    }

    /// <summary>
    /// The cleaner's only row in the period that closes is a charge, so there is nothing to invoice and no pay
    /// to announce: no invoice, no "period closed" e-mail, and the charge waits in the successor.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_Whose_Only_Row_Is_A_Charge_Gets_No_Email_And_The_Charge_Moves_On()
    {
        string expiredId = default!;

        await TestMethod(
            setup: RegisterBatch,
            arrange: async context =>
            {
                var job = await SeedInvoicedJobAsync(context);
                var expired = PayPeriod.Create(Today.AddDays(-14), Today.AddDays(-2));
                context.Add(expired);
                await context.CommitAsync(CancellationToken.None);
                context.Add(job.CarryDisputeCharge(expired.Id, _disputeId, 150m, Reason));
                await context.CommitAsync(CancellationToken.None);
                expiredId = expired.Id;
            },
            act: async provider =>
            {
                await provider.GetRequiredService<IPayPeriodBackgroundService>()
                    .CloseExpiredPeriodsAndOpenNewAsync(CancellationToken.None);
                return true;
            },
            assert: async (context, _) =>
            {
                var successor = await context.PayPeriods.IgnoreQueryFilters().SingleAsync(p => p.Status == PayPeriodStatus.Open);
                var charge = await context.OrderEmployeePays.IgnoreQueryFilters()
                    .SingleAsync(p => p.LineType == PayLineType.DisputeCharge);
                Assert.Equal((successor.Id, (string?)null), (charge.PayPeriodId, charge.EmployeeInvoiceId));
                Assert.False(await context.EmployeeInvoices.IgnoreQueryFilters().AnyAsync(i => i.PayPeriodId == expiredId));
                _email.Verify(e => e.SendPeriodClosedEmailAsync(It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<byte[]?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            },
            transactional: false);
    }

    private Task RegisterBatch(IServiceCollection services)
    {
        // The batch is registered by the Functions host, not by AddCoreBindings.
        services.AddScoped<PayPeriodBackgroundService>();
        services.AddScoped<IPayPeriodBackgroundService>(sp => sp.GetRequiredService<PayPeriodBackgroundService>());
        services.Replace(ServiceDescriptor.Singleton(_email.Object));
        return Task.CompletedTask;
    }

    private ResolveDispute.Command ResolveWithCharge(decimal amount) =>
        new(_disputeId, null, "The cleaner was at fault.", new ResolveDispute.CleanerCharge(_employeeId, amount, Reason));

    private static async Task<TResult> SendAsync<TResult>(IServiceProvider provider, IRequest<TResult> request)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    /// <summary>The cleaner, job J1 invoiced at 600 in a closed period, and the customer's dispute on J1.</summary>
    private async Task<OrderEmployeePay> SeedInvoicedJobAsync(CleansiaDbContext context)
    {
        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        context.Languages.Add(Language.Create("en", "English"));
        context.Countries.Add(country);
        context.Currencies.Add(currency);

        var invoicedPeriod = PayPeriod.Create(Today.AddDays(-40), Today.AddDays(-27)).Close("System");
        context.Add(invoicedPeriod);

        var user = User.CreateWithPassword("carried-charge@cleansia.test", "12345678Test!", "Emp", "Carried", UserProfile.Employee);
        user.ConfirmEmail();
        context.Users.Add(user);
        var employee = Employee.CreateWithUser(user);
        context.Add(employee);

        var order = NewOrder("carried-disputed@cleansia.test");
        context.Add(order);
        var dispute = new Dispute(order.Id, null, DisputeReason.QualityIssue, "Bathroom not cleaned", "customer");
        context.Add(dispute);
        await context.CommitAsync(CancellationToken.None);

        var job = OrderEmployeePay.Create(order.Id, employee.Id, invoicedPeriod.Id, CurrencyId, basePay: 600m, totalPay: 600m);
        var invoice = EmployeeInvoice.CreateFromOrderPays(employee.Id, invoicedPeriod.Id, [job],
            PayrollMockFactory.NextTestVariableSymbol(), PayrollMockFactory.NextTestInvoiceNumber());
        job.AssignToInvoice(invoice.Id);
        context.AddRange(invoice, job);
        await context.CommitAsync(CancellationToken.None);

        _employeeId = employee.Id;
        _invoicedOrderId = order.Id;
        _disputeId = dispute.Id;
        return job;
    }

    private static Order NewOrder(string customerEmail)
    {
        var order = Order.Create(
            customerName: "Carried Customer",
            customerEmail: customerEmail,
            customerPhone: "+420777333444",
            customerAddress: Address.Create("Order St 9", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-30),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        return order;
    }
}
