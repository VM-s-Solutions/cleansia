using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Functions.Core;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

[Collection("PostgresCollection")]
public sealed class PayPeriodEmployeeIsolationPostgresTests(PostgresContainerFixture fixture)
    : BaseIntegrationTest(fixture)
{
    private const string PeriodId = "isolation-pay-period";

    [Theory]
    [InlineData("invoice")]
    [InlineData("pdf-success")]
    [InlineData("pdf-error")]
    public Task A_Refused_Employee_Write_Is_Discarded_Before_B_And_The_Plain_Fallback(string mode) => RunAsync(mode);

    [Fact]
    public Task Active_Host_Cancellation_Stops_Before_B_With_The_Close_Already_Durable() => RunAsync("cancel");

    private async Task RunAsync(string mode)
    {
        using var stop = new CancellationTokenSource();
        var observations = new ObserveCommits(Fixture.GetConnectionString());
        var emails = new List<(string Email, bool Document, int DirtyInvoices)>();
        var uploads = new List<string>();
        await TestMethod<bool>(
            setup: services =>
            {
                services.AddFunctionsProcessing();
                services.AddDbContext<CleansiaDbContext>((_, options) => options.AddInterceptors(observations));
                var pdf = new Mock<IPdfService>();
                pdf.Setup(p => p.GenerateInvoicePdf(It.IsAny<InvoicePdfData>(), It.IsAny<CountryInvoiceContext?>(), It.IsAny<string?>()))
                    .Returns<InvoicePdfData, CountryInvoiceContext?, string?>((data, _, _) =>
                    {
                        if (mode == "cancel" && data.Supplier.Email == "a@payroll.test")
                        {
                            stop.Cancel();
                            throw new OperationCanceledException(stop.Token);
                        }
                        return mode == "pdf-error" && data.Supplier.Email == "a@payroll.test"
                            ? throw new InvalidOperationException("offline A renderer refused") : [1, 2, 3];
                    });
                services.Replace(ServiceDescriptor.Singleton(pdf.Object));
                var blob = new Mock<IBlobContainerClient>();
                blob.Setup(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
                    .Callback<string, Stream, Metadata?, CancellationToken>((name, _, _, _) => uploads.Add(name))
                    .Returns(Task.CompletedTask);
                blob.Setup(b => b.GetBlobUri(It.IsAny<string>())).Returns<string>(name => new Uri($"https://blobs.test/{name}"));
                var blobs = new Mock<IBlobContainerClientFactory>();
                blobs.Setup(b => b.GetBlobContainerClient(It.IsAny<string>())).Returns(blob.Object);
                services.Replace(ServiceDescriptor.Singleton(blobs.Object));
                services.Replace(ServiceDescriptor.Scoped<IEmailService>(sp =>
                {
                    var context = sp.GetRequiredService<CleansiaDbContext>();
                    var email = new Mock<IEmailService>();
                    email.Setup(e => e.SendPeriodClosedEmailAsync(It.IsAny<string>(), It.IsAny<string>(),
                            It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                            It.IsAny<string>(), It.IsAny<byte[]?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                        .Returns<string, string, DateOnly, DateOnly, DateTime, string, string, byte[]?, string?, CancellationToken>(
                            async (address, _, _, _, _, _, _, bytes, _, ct) =>
                            {
                                await observations.ObserveAsync(context, "email", ct);
                                emails.Add((address, bytes is not null, context.ChangeTracker.Entries<EmployeeInvoice>()
                                    .Count(i => i.State is EntityState.Added or EntityState.Modified)));
                                return "offline-email";
                            });
                    return email.Object;
                }));
                return Task.CompletedTask;
            },
            arrange: async context =>
            {
                await SeedAsync(context);
                var sql = mode switch
                {
                    "invoice" => """ALTER TABLE "CashLedgerEntries" ADD CONSTRAINT "test_payroll_a_refusal" CHECK ("EmployeeId" <> 'payroll-a' OR "Kind" <> 4)""",
                    "pdf-success" => """ALTER TABLE "EmployeeInvoices" ADD CONSTRAINT "test_payroll_a_refusal" CHECK ("EmployeeId" <> 'payroll-a' OR "PdfBlobUrl" IS NULL)""",
                    _ => """ALTER TABLE "EmployeeInvoices" ADD CONSTRAINT "test_payroll_a_refusal" CHECK ("EmployeeId" <> 'payroll-a' OR NOT "PdfGenerationFailed")"""
                };
                if (mode != "cancel") await context.Database.ExecuteSqlRawAsync(sql);
            },
            act: async provider =>
            {
                var failure = await Record.ExceptionAsync(() => provider.GetRequiredService<IPayPeriodBackgroundService>()
                    .CloseExpiredPeriodsAndOpenNewAsync(stop.Token));
                if (mode == "cancel")
                {
                    await using var cancelled = provider.CreateAsyncScope();
                    var context = cancelled.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                    var period = await context.PayPeriods.AsNoTracking().SingleAsync(p => p.Id == PeriodId);
                    var invoices = await context.EmployeeInvoices.AsNoTracking().ToListAsync();
                    Console.WriteLine(JsonSerializer.Serialize(new { phase = "host-cancelled", period.Status,
                        invoices = invoices.Select(i => new { i.EmployeeId, i.VariableSymbol, i.PdfBlobUrl }), observations.Rows, emails, uploads }));
                    Assert.IsAssignableFrom<OperationCanceledException>(failure);
                    Assert.Equal(PayPeriodStatus.Closed, period.Status);
                    Assert.Equal("payroll-a", Assert.Single(invoices).EmployeeId);
                    Assert.Null(invoices.Single().PdfBlobUrl);
                    Assert.Empty(emails);
                    Assert.Empty(uploads);
                    return true;
                }
                await using (var check = provider.CreateAsyncScope())
                {
                    var context = check.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                    await AssertRowsAsync(context, mode, observations, emails, uploads, "first-refusal");
                }
                Assert.Null(failure);
                if (mode == "invoice")
                {
                    await using (var reopen = provider.CreateAsyncScope())
                    {
                        var context = reopen.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                        (await context.PayPeriods.SingleAsync(p => p.Id == PeriodId)).Reopen();
                        await context.CommitAsync(CancellationToken.None);
                    }
                    await using var repeat = provider.CreateAsyncScope();
                    await repeat.ServiceProvider.GetRequiredService<IPayPeriodBackgroundService>().CloseExpiredPeriodsAndOpenNewAsync();
                }
                return true;
            },
            assert: async (context, _) =>
            {
                if (mode == "cancel") return;
                await AssertRowsAsync(context, mode, observations, emails, uploads, "final-refusal");
                Assert.Equal(PayPeriodStatus.Closed, (await context.PayPeriods.AsNoTracking().SingleAsync(p => p.Id == PeriodId)).Status);
                Assert.All(observations.Rows, row => Assert.Equal(PayPeriodStatus.Closed, row.DurableStatus));
                Assert.All(emails.Where(e => !e.Document), e => Assert.Equal(0, e.DirtyInvoices));
                Assert.Contains(emails, e => e.Email == "b@payroll.test" && e.Document);
                Assert.Equal(2, observations.Rows.Count(r => r.EmployeeIds.Contains("payroll-a")));
            },
            cleanup: async context =>
            {
                var sql = mode == "invoice"
                    ? """ALTER TABLE "CashLedgerEntries" DROP CONSTRAINT IF EXISTS "test_payroll_a_refusal";"""
                    : """ALTER TABLE "EmployeeInvoices" DROP CONSTRAINT IF EXISTS "test_payroll_a_refusal";""";
                await context.Database.ExecuteSqlRawAsync(sql);
                var left = await context.Database.SqlQueryRaw<int>(
                    """SELECT COUNT(*)::int AS "Value" FROM pg_constraint WHERE conname = 'test_payroll_a_refusal'""").SingleAsync();
                Console.WriteLine(JsonSerializer.Serialize(new { phase = "fault-constraint-cleaned", mode, remaining = left }));
                Assert.Equal(0, left);
            }, transactional: false);
    }

    [Fact]
    public async Task A_Successor_Read_Failure_Leaves_The_Staged_Close_Open_For_A_Fresh_Tick()
    {
        var fault = new SuccessorReadFault();
        await TestMethod<bool>(
            setup: services =>
            {
                services.AddFunctionsProcessing();
                services.Replace(ServiceDescriptor.Scoped<IPayPeriodRepository>(sp =>
                    new RefuseSuccessorRead(sp.GetRequiredService<CleansiaDbContext>(), fault)));
                return Task.CompletedTask;
            },
            arrange: async context =>
            {
                var period = PayPeriod.CreateBiWeekly(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)));
                period.Id = PeriodId;
                context.Add(period);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var failure = await Record.ExceptionAsync(() => provider.GetRequiredService<IPayPeriodBackgroundService>()
                    .CloseExpiredPeriodsAndOpenNewAsync());
                await using (var check = provider.CreateAsyncScope())
                {
                    var context = check.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                    var period = await context.PayPeriods.AsNoTracking().SingleAsync(p => p.Id == PeriodId);
                    Console.WriteLine(JsonSerializer.Serialize(new { phase = "successor-staging-refused", period.Id, period.Status, error = failure?.GetType().Name }));
                    Assert.Equal(PayPeriodStatus.Open, period.Status);
                }
                Assert.IsType<InvalidOperationException>(failure);
                fault.Enabled = false;
                await using var retry = provider.CreateAsyncScope();
                await retry.ServiceProvider.GetRequiredService<IPayPeriodBackgroundService>().CloseExpiredPeriodsAndOpenNewAsync();
                return true;
            },
            assert: async (context, _) =>
            {
                var periods = await context.PayPeriods.AsNoTracking().ToListAsync();
                Assert.Equal(PayPeriodStatus.Closed, periods.Single(p => p.Id == PeriodId).Status);
                Assert.Single(periods, p => p.Status == PayPeriodStatus.Open);
            }, transactional: false);
    }

    private static async Task AssertRowsAsync(CleansiaDbContext context, string mode, ObserveCommits observations,
        List<(string Email, bool Document, int DirtyInvoices)> emails, List<string> uploads, string phase)
    {
        var invoices = await context.EmployeeInvoices.AsNoTracking().OrderBy(i => i.EmployeeId).ToListAsync();
        var pays = await context.OrderEmployeePays.AsNoTracking().OrderBy(p => p.EmployeeId).ToListAsync();
        var entries = await context.Set<CashLedgerEntry>().AsNoTracking().Where(e => e.Kind == CashLedgerEntryKind.SetOff).ToListAsync();
        Console.WriteLine(JsonSerializer.Serialize(new { phase, mode,
            invoices = invoices.Select(i => new { i.EmployeeId, i.VariableSymbol, i.InvoiceNumber, i.CashSetOffAmount, i.PdfBlobUrl, i.PdfGenerationFailed, i.PdfGenerationError }),
            pays = pays.Select(p => new { p.EmployeeId, p.EmployeeInvoiceId }),
            entries = entries.Select(e => new { e.EmployeeId, e.Amount }), observations.Rows,
            emails = emails.Select(e => new { e.Email, e.Document, e.DirtyInvoices }), uploads }));
        var b = Assert.Single(invoices, i => i.EmployeeId == "payroll-b");
        Assert.NotNull(b.PdfBlobUrl);
        Assert.False(b.PdfGenerationFailed);
        Assert.Equal(100m, b.CashSetOffAmount);
        Assert.Equal(b.Id, pays.Single(p => p.EmployeeId == "payroll-b").EmployeeInvoiceId);
        Assert.Matches("^[1-9][0-9]{9}$", b.VariableSymbol!);
        Assert.Matches("^INV-[0-9]{4}-[0-9]{6}$", b.InvoiceNumber);
        if (mode == "invoice")
        {
            Assert.DoesNotContain(invoices, i => i.EmployeeId == "payroll-a");
            Assert.Null(pays.Single(p => p.EmployeeId == "payroll-a").EmployeeInvoiceId);
            Assert.Equal("payroll-b", Assert.Single(entries).EmployeeId);
        }
        else
        {
            var a = Assert.Single(invoices, i => i.EmployeeId == "payroll-a");
            Assert.Null(a.PdfBlobUrl);
            Assert.False(a.PdfGenerationFailed);
            Assert.Null(a.PdfGenerationError);
            Assert.Equal(2, entries.Count);
        }
    }

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = "payroll-country";
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = "payroll-currency";
        context.AddRange(country, currency, Language.Create("en", "English"),
            CompanyInfo.Create("Cleansia", "Cleansia", "12345678", "Street 1", "Prague", "11000", country.Id));
        var period = PayPeriod.CreateBiWeekly(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)));
        period.Id = PeriodId;
        context.Add(period);
        foreach (var id in new[] { "a", "b" })
        {
            var user = User.CreateWithPassword($"{id}@payroll.test", "Test-password-1!", id, "Cleaner", UserProfile.Employee);
            var employee = Employee.CreateWithUser(user);
            employee.Id = $"payroll-{id}";
            employee.UpdateAddress(Address.Create("Home 1", "Prague", "11000", country.Id));
            context.Add(employee);
            var order = Order.Create("Customer", $"customer-{id}@payroll.test", "+420777444556",
                Address.Create("Job 1", "Prague", "11000", country.Id), 2, 1,
                DateTime.UtcNow.AddDays(-20), PaymentType.Cash, 1200m, currency.Id,
                paymentStatus: PaymentStatus.Pending, cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            order.Id = $"payroll-order-{id}";
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
            order.MarkCashCollected(employee.Id, amount: 100m);
            context.Add(order);
            context.Add(CashLedgerEntry.ForCollection(order));
            context.Add(OrderEmployeePay.Create(order.Id, employee.Id, period.Id, currency.Id, basePay: 600m, totalPay: 600m));
        }
        await context.CommitAsync(CancellationToken.None);
    }

    private sealed class ObserveCommits(string connectionString) : SaveChangesInterceptor
    {
        public List<Observation> Rows { get; } = [];
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<EmployeeInvoice>().Any(i => i.State is EntityState.Added or EntityState.Modified))
                await ObserveAsync(eventData.Context, "invoice-save", cancellationToken);
            return result;
        }
        public async Task ObserveAsync(DbContext context, string phase, CancellationToken ct)
        {
            await using var check = new CleansiaDbContext(new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(connectionString).Options);
            var status = await check.PayPeriods.IgnoreQueryFilters().AsNoTracking().Where(p => p.Id == PeriodId).Select(p => p.Status).SingleAsync(ct);
            Rows.Add(new Observation(phase, status, context.ChangeTracker.Entries<PayPeriod>().Select(p => p.Entity.Status).ToArray(),
                context.ChangeTracker.Entries<EmployeeInvoice>().Where(i => i.State is EntityState.Added or EntityState.Modified).Select(i => i.Entity.EmployeeId).Distinct().ToArray()));
        }
    }
    private sealed record Observation(string Phase, PayPeriodStatus DurableStatus, PayPeriodStatus[] TrackedStatuses, string[] EmployeeIds);
    private sealed class SuccessorReadFault
    {
        public bool Enabled { get; set; } = true;
    }
    private sealed class RefuseSuccessorRead(CleansiaDbContext context, SuccessorReadFault fault) : PayPeriodRepository(context)
    {
        public override IQueryable<PayPeriod> GetQueryable() => fault.Enabled
            ? throw new InvalidOperationException("successor read refused after close staging") : base.GetQueryable();
    }
}
