using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// ADR-0068 D5 (Verification #7) — both subjects' halves in the export: the cleaner's own rows, full
/// (the job, the seat, the exact text, the instant, the client, the trio and the facts), and on the
/// customer's orders the version the order was booked under with each acceptance's instant, version
/// and language — and no cleaner id, the counterparty's identity being the platform's to hold.
/// </summary>
public sealed class WorkContractExportTests : IDisposable
{
    private const string CleanerUserId = "user-export-wc-cleaner";
    private const string CleanerEmployeeId = "employee-export-wc-1";
    private const string OtherEmployeeId = "employee-export-wc-2";
    private const string CustomerUserId = "user-export-wc-customer";
    private const string OrderId = "order-export-wc-1";
    private const string OtherOrderId = "order-export-wc-2";

    private readonly SqliteConnection _connection;

    public WorkContractExportTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task The_Cleaners_Export_Lists_Their_Rows_With_The_Trio_And_The_Facts_And_Nobody_Elses()
    {
        await SeedAsync();

        var export = await ExportAsync(CleanerUserId);

        var row = Assert.Single(export.WorkContractAcceptances);
        Assert.Equal(OrderId, row.OrderId);
        Assert.Equal("ORD-EXPORT-1", row.OrderNumber);
        Assert.Equal("seat-export-1", row.OrderEmployeeId);
        Assert.Equal(WorkContractTestData.TextIdCs, row.LegalDocumentTextId);
        Assert.Equal(WorkContractTestData.Version, row.DocumentVersion);
        Assert.Equal("cs", row.Language);
        Assert.Equal("cleansia.mobile", row.ClientAudience);
        Assert.Equal("203.0.113.9", row.IpAddress);
        Assert.Equal("Pixel 8", row.DeviceLabel);
        Assert.Equal("device-export", row.DeviceId);
        Assert.Contains("\"orderNumber\":\"ORD-EXPORT-1\"", row.FactsJson);
    }

    [Fact]
    public async Task The_Customers_Export_Lists_The_Orders_Version_And_Each_Acceptances_Instant_Version_And_Language_Without_An_Employee_Id()
    {
        await SeedAsync();

        var export = await ExportAsync(CustomerUserId);

        Assert.Empty(export.WorkContractAcceptances);
        var order = Assert.Single(export.Orders, o => o.Id == OrderId);
        Assert.Equal(WorkContractTestData.Version, order.WorkContractDocumentVersion);
        var acceptances = order.WorkContractAcceptances!;
        Assert.Equal(2, acceptances.Count);
        Assert.Equal(["cs", "en"], acceptances.Select(a => a.Language).OrderBy(l => l));
        Assert.All(acceptances, a => Assert.Equal(WorkContractTestData.Version, a.DocumentVersion));
        Assert.DoesNotContain(typeof(Core.AppServices.Features.Gdpr.DTOs.GdprExportOrderWorkContractAcceptanceDto).GetProperties(),
            p => p.Name.Contains("Employee", StringComparison.Ordinal));

        var unbooked = Assert.Single(export.Orders, o => o.Id == OtherOrderId);
        Assert.Null(unbooked.WorkContractDocumentVersion);
        Assert.Empty(unbooked.WorkContractAcceptances!);
    }

    private async Task<Core.AppServices.Features.Gdpr.DTOs.GdprExportDto> ExportAsync(string userId)
    {
        await using var ctx = NewContext();
        var consents = new Mock<IUserConsentRepository>();
        consents.Setup(r => r.GetByUserIdNoTrackingAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var service = new GdprExportService(
            new UserRepository(ctx),
            new OrderRepository(ctx),
            new DisputeRepository(ctx),
            new EmployeeDocumentRepository(ctx),
            new EmployeeInvoiceRepository(ctx),
            new EmployeePayoutDetailsRepository(ctx),
            consents.Object,
            new CustomerActionAuditRepository(ctx),
            new WorkContractAcceptanceRepository(ctx),
            new LegalDocumentRepository(ctx));

        return await service.BuildAsync(userId, exportedBy: "self", CancellationToken.None);
    }

    private async Task SeedAsync()
    {
        await using (var schema = NewContext())
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var ctx = NewContext();

        var cleanerUser = User.CreateWithPassword("cleaner.export@cleansia.test", "Test-password-1!", "Petra", "Svobodova", UserProfile.Employee);
        cleanerUser.Id = CleanerUserId;
        var cleaner = Employee.CreateWithUser(cleanerUser);
        cleaner.Id = CleanerEmployeeId;
        ctx.Add(cleaner);

        var otherUser = User.CreateWithPassword("other.export@cleansia.test", "Test-password-1!", "Karel", "Novak", UserProfile.Employee);
        otherUser.Id = $"{OtherEmployeeId}-user";
        var other = Employee.CreateWithUser(otherUser);
        other.Id = OtherEmployeeId;
        ctx.Add(other);

        var customer = User.CreateWithPassword("customer.export@cleansia.test", "Test-password-1!", "Jana", "Customer", UserProfile.Customer);
        customer.Id = CustomerUserId;
        ctx.Add(customer);

        var document = WorkContractTestData.Document();
        ctx.LegalDocuments.Add(document);

        var booked = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            Id = OrderId,
            UserId = CustomerUserId,
            CustomerAddress = Address.Create("Dlouha 14", "Praha", "11000", "CZ"),
            CurrentStatus = OrderStatus.Completed,
        });
        booked.SetWorkContractDocument(document);
        SetOrderNumber(booked, "ORD-EXPORT-1");
        ctx.Add(booked);

        ctx.Add(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            Id = OtherOrderId,
            UserId = CustomerUserId,
            CustomerAddress = Address.Create("Kratka 2", "Brno", "60200", "CZ"),
            CurrentStatus = OrderStatus.Completed,
        }));

        ctx.Add(WorkContractAcceptance.Create(
            OrderId, "seat-export-1", CleanerEmployeeId, document.TextFor("cs")!, WorkContractTestData.Version,
            "cleansia.mobile", "203.0.113.9", "Pixel 8", "device-export", "{\"orderNumber\":\"ORD-EXPORT-1\"}"));
        ctx.Add(WorkContractAcceptance.Create(
            OrderId, "seat-export-2", OtherEmployeeId, document.TextFor("en")!, WorkContractTestData.Version,
            "cleansia.partner", "198.51.100.7", "Firefox", null, "{\"orderNumber\":\"ORD-EXPORT-1\"}"));

        await ctx.CommitAsync(CancellationToken.None);
    }

    private static void SetOrderNumber(Order order, string number) =>
        typeof(Order).GetProperty(nameof(Order.DisplayOrderNumber))!.SetValue(order, number);

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
