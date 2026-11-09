using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// The 5-minute sweep's invoice candidates on Postgres. Pay is invoiced once its period has closed, so
/// an open period is never a candidate however long it has run, and a closed one waits out the
/// threshold from its close, which leaves the close itself to invoice first.
/// </summary>
[Collection("PostgresCollection")]
public class InvoiceReconciliationCandidatesTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-recon";
    private const string CurrencyId = "currency-czk-recon";

    [Fact]
    public async Task Only_A_Period_Closed_Before_The_Threshold_Yields_Candidates()
    {
        string employeeId = default!;
        string closedEarlier = default!;
        string closedJustNow = default!;
        string running = default!;

        await TestMethod(
            arrange: async context =>
            {
                employeeId = await SeedCleanerAsync(context);
                closedEarlier = await SeedPeriodWithPayAsync(context, employeeId, new DateOnly(2026, 1, 1), closedAt: DateTime.UtcNow.AddMinutes(-30));
                closedJustNow = await SeedPeriodWithPayAsync(context, employeeId, new DateOnly(2026, 1, 15), closedAt: DateTime.UtcNow.AddMinutes(-5));
                running = await SeedPeriodWithPayAsync(context, employeeId, new DateOnly(2026, 1, 29), closedAt: null);
            },
            act: async provider => await provider.GetRequiredService<IPayPeriodRepository>()
                .GetInvoiceReconciliationCandidatesAsync(DateTime.UtcNow.AddMinutes(-15), take: 50, CancellationToken.None),
            assert: (CleansiaDbContext _, List<InvoiceReconciliationItem> candidates) =>
            {
                Assert.DoesNotContain(candidates, c => c.PayPeriodId == running);
                Assert.DoesNotContain(candidates, c => c.PayPeriodId == closedJustNow);
                var candidate = Assert.Single(candidates);
                Assert.Equal((closedEarlier, employeeId, TestTenants.Default), (candidate.PayPeriodId, candidate.EmployeeId, candidate.TenantId));
                return Task.CompletedTask;
            });
    }

    /// <summary>A Paid period whose pay is still un-invoiced stays a candidate.</summary>
    [Fact]
    public async Task A_Period_Marked_Paid_Before_The_Sweep_Is_Still_A_Candidate()
    {
        string employeeId = default!;
        string paid = default!;

        await TestMethod(
            arrange: async context =>
            {
                employeeId = await SeedCleanerAsync(context);
                paid = await SeedPeriodWithPayAsync(context, employeeId, new DateOnly(2026, 1, 1), closedAt: DateTime.UtcNow.AddMinutes(-30), markPaid: true);
            },
            act: async provider => await provider.GetRequiredService<IPayPeriodRepository>()
                .GetInvoiceReconciliationCandidatesAsync(DateTime.UtcNow.AddMinutes(-15), take: 50, CancellationToken.None),
            assert: (CleansiaDbContext _, List<InvoiceReconciliationItem> candidates) =>
            {
                var candidate = Assert.Single(candidates);
                Assert.Equal((paid, employeeId), (candidate.PayPeriodId, candidate.EmployeeId));
                return Task.CompletedTask;
            });
    }

    private static async Task<string> SeedCleanerAsync(CleansiaDbContext context)
    {
        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        context.Languages.Add(Language.Create("en", "English"));
        context.Countries.Add(country);
        context.Currencies.Add(currency);

        var user = User.CreateWithPassword("recon-emp@cleansia.test", "12345678Test!", "Emp", "Recon", UserProfile.Employee);
        user.ConfirmEmail();
        context.Users.Add(user);
        var employee = Employee.CreateWithUser(user);
        context.Add(employee);
        await context.CommitAsync(CancellationToken.None);
        return employee.Id;
    }

    private static async Task<string> SeedPeriodWithPayAsync(CleansiaDbContext context, string employeeId, DateOnly start, DateTime? closedAt, bool markPaid = false)
    {
        var period = PayPeriod.CreateBiWeekly(start);
        // Created long before the threshold, so only the status and the close can keep it out.
        period.Created("system", DateTimeOffset.UtcNow.AddDays(-30));
        if (closedAt is not null)
        {
            period.Close("System");
            typeof(PayPeriod).GetProperty(nameof(PayPeriod.ClosedAt))!.SetValue(period, closedAt);
        }
        if (markPaid)
        {
            period.MarkAsPaid();
        }
        context.Add(period);

        var order = Order.Create(
            customerName: "Recon Customer",
            customerEmail: $"recon-{start:yyyyMMdd}@cleansia.test",
            customerPhone: "+420777333444",
            customerAddress: Address.Create("Order St 9", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-3),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        context.Add(order);
        await context.CommitAsync(CancellationToken.None);

        context.Add(OrderEmployeePay.Create(order.Id, employeeId, period.Id, CurrencyId, basePay: 600m, totalPay: 600m));
        await context.CommitAsync(CancellationToken.None);
        return period.Id;
    }
}
