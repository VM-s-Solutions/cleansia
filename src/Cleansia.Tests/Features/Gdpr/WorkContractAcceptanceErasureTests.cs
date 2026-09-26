using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Configuration;
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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// ADR-0068 D5 (Verification #6) — a cleaner's erasure keeps every acceptance row with its order, seat,
/// employee handle, text and facts, and blanks the IP address, device label and device id on each, in
/// the erasure's own commit: staged, not saved, until the walk's single commit lands — so a throw after
/// the staging leaves the trio intact. A bystander's rows, and the rows on a customer's orders when the
/// CUSTOMER is erased, are untouched.
/// </summary>
public sealed class WorkContractAcceptanceErasureTests : IDisposable
{
    private const string ErasedUserId = "user-erase-wc-1";
    private const string ErasedEmployeeId = "employee-erase-wc-1";
    private const string BystanderUserId = "user-keep-wc-1";
    private const string BystanderEmployeeId = "employee-keep-wc-1";
    private const string CustomerUserId = "user-customer-wc-1";
    private const string OrderA = "order-erase-wc-a";
    private const string OrderB = "order-erase-wc-b";

    private readonly SqliteConnection _connection;
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();

    public WorkContractAcceptanceErasureTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();

        _blobClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(Mock.Of<IBlobContainerClient>());
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task The_Cleaners_Erasure_Blanks_The_Trio_On_Every_Row_And_Keeps_The_Evidence()
    {
        await SeedAsync();
        var before = await ReadAsync();
        Assert.Equal(2, before.Count(a => a.EmployeeId == ErasedEmployeeId && a.IpAddress != null && a.DeviceLabel != null && a.DeviceId != null));

        await EraseAsync(ErasedUserId);

        var after = await ReadAsync();
        var erased = after.Where(a => a.EmployeeId == ErasedEmployeeId).ToList();
        Assert.Equal(2, erased.Count);
        Assert.All(erased, a =>
        {
            Assert.Null(a.IpAddress);
            Assert.Null(a.DeviceLabel);
            Assert.Null(a.DeviceId);
            Assert.Equal(WorkContractTestData.TextIdEn, a.LegalDocumentTextId);
            Assert.Contains("\"orderNumber\"", a.FactsJson);
            Assert.Equal(WorkContractTestData.Version, a.DocumentVersion);
        });
        Assert.Equal([OrderA, OrderB], erased.Select(a => a.OrderId).OrderBy(x => x));
        Assert.All(erased, a => Assert.StartsWith("seat-", a.OrderEmployeeId));

        var bystander = Assert.Single(after, a => a.EmployeeId == BystanderEmployeeId);
        Assert.Equal("198.51.100.7", bystander.IpAddress);
        Assert.Equal("iPhone 15", bystander.DeviceLabel);
        Assert.Equal("device-keep", bystander.DeviceId);
    }

    [Fact]
    public async Task The_Blanking_Rides_The_Erasures_Commit_And_A_Throw_After_The_Staging_Leaves_The_Trio_Intact()
    {
        await SeedAsync();

        var customerAudits = new Mock<ICustomerActionAuditRepository>();
        customerAudits
            .Setup(r => r.PseudonymiseForSubjectAsync(ErasedUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("commit path broken"));

        await using (var ctx = NewContext())
        {
            var service = BuildService(ctx, ErasedUserId, customerAudits.Object);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteUserAccountAsync(
                ErasedUserId, "gdpr_erasure_test", _ => ("test-actor", null), deferEmployeeErasure: false, CancellationToken.None));

            // Staged in this context: the rows are tracked and blanked, and nothing is saved.
            var staged = ctx.ChangeTracker.Entries<WorkContractAcceptance>().Where(e => e.State == EntityState.Modified).ToList();
            Assert.Equal(2, staged.Count);
        }

        var persisted = await ReadAsync();
        Assert.All(persisted.Where(a => a.EmployeeId == ErasedEmployeeId), a =>
        {
            Assert.Equal("203.0.113.9", a.IpAddress);
            Assert.Equal("Pixel 8", a.DeviceLabel);
            Assert.Equal("device-erased", a.DeviceId);
        });
    }

    [Fact]
    public async Task The_Customers_Erasure_Leaves_The_Rows_On_Their_Orders_Untouched()
    {
        await SeedAsync();

        await EraseAsync(CustomerUserId);

        var after = await ReadAsync();
        Assert.Equal(3, after.Count);
        Assert.All(after, a =>
        {
            Assert.NotNull(a.IpAddress);
            Assert.NotNull(a.DeviceLabel);
            Assert.NotNull(a.DeviceId);
        });
        var order = await ReadOrderAsync(OrderA);
        Assert.NotEqual("Customer Jana", order.CustomerName);
    }

    private async Task EraseAsync(string userId)
    {
        await using var ctx = NewContext();
        var service = BuildService(ctx, userId, new CustomerActionAuditRepository(ctx));

        var result = await service.DeleteUserAccountAsync(
            userId, "gdpr_erasure_test", _ => ("test-actor", null), deferEmployeeErasure: false, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private GdprDeletionService BuildService(CleansiaDbContext ctx, string userId, ICustomerActionAuditRepository customerAudits)
    {
        var session = new TestUserSessionProvider(userId, $"{userId}@cleansia.test");
        return new GdprDeletionService(
            new UserRepository(ctx),
            new OrderRepository(ctx),
            new EmployeeDocumentRepository(ctx),
            new DocumentDeletionRequestRepository(ctx),
            new EmployeeInvoiceRepository(ctx),
            new CreditAccountRepository(ctx),
            new EmployeePayoutDetailsRepository(ctx),
            new UserMembershipRepository(ctx),
            new UserStripeCustomerRepository(ctx),
            new OrderPhotoRepository(ctx),
            new DeviceRepository(ctx, session),
            new LiveActivityTokenRepository(ctx),
            new UserConsentRepository(ctx),
            new GdprRequestRepository(ctx),
            new DisputeRepository(ctx),
            new SavedAddressRepository(ctx, session),
            new OrderEmployeePayRepository(ctx),
            new RecurringBookingTemplateRepository(ctx),
            new UserNotificationRepository(ctx),
            new DeadLetterRepository(ctx),
            new OutboxMessageRepository(ctx),
            customerAudits,
            new WorkContractAcceptanceRepository(ctx),
            new AddressRepository(ctx),
            new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(ctx)),
            Mock.Of<IRefreshTokenService>(),
            Mock.Of<IStripeClient>(),
            _blobClientFactory.Object,
            Mock.Of<IAppConfigurationProvider>(),
            new ErasureAttempt(),
            new ArchiveWriteGate(),
            NullLogger<GdprDeletionService>.Instance);
    }

    private async Task<List<WorkContractAcceptance>> ReadAsync()
    {
        await using var ctx = NewContext();
        return await ctx.WorkContractAcceptances.IgnoreQueryFilters().AsNoTracking().ToListAsync();
    }

    private async Task<Order> ReadOrderAsync(string orderId)
    {
        await using var ctx = NewContext();
        return await ctx.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == orderId);
    }

    private async Task SeedAsync()
    {
        await using (var schema = NewContext())
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var ctx = NewContext();

        ctx.Add(NewUserWithEmployee(ErasedUserId, ErasedEmployeeId, "erased.cleaner@cleansia.test", "Milada", "Novotna"));
        ctx.Add(NewUserWithEmployee(BystanderUserId, BystanderEmployeeId, "kept.cleaner@cleansia.test", "Tomas", "Svoboda"));
        var customer = User.CreateWithPassword("customer.wc@cleansia.test", "Test-password-1!", "Customer", "Jana", UserProfile.Customer);
        customer.Id = CustomerUserId;
        ctx.Add(customer);

        ctx.Add(NewOrder(CustomerUserId, OrderA));
        ctx.Add(NewOrder(CustomerUserId, OrderB));
        ctx.LegalDocuments.Add(WorkContractTestData.Document());

        ctx.Add(Acceptance(OrderA, "seat-erased-a", ErasedEmployeeId, "203.0.113.9", "Pixel 8", "device-erased"));
        ctx.Add(Acceptance(OrderB, "seat-erased-b", ErasedEmployeeId, "203.0.113.9", "Pixel 8", "device-erased"));
        ctx.Add(Acceptance(OrderB, "seat-kept-b", BystanderEmployeeId, "198.51.100.7", "iPhone 15", "device-keep"));

        await ctx.CommitAsync(CancellationToken.None);
    }

    private static WorkContractAcceptance Acceptance(
        string orderId, string seatId, string employeeId, string ip, string deviceLabel, string deviceId) =>
        WorkContractAcceptance.Create(
            orderId, seatId, employeeId, WorkContractTestData.Document().TextFor("en")!, WorkContractTestData.Version,
            "cleansia.mobile", ip, deviceLabel, deviceId, "{\"orderNumber\":\"ORD-WC\"}");

    private static Employee NewUserWithEmployee(
        string userId, string employeeId, string email, string firstName, string lastName)
    {
        var user = User.CreateWithPassword(email, "Test-password-1!", firstName, lastName, UserProfile.Employee);
        user.Id = userId;

        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;

        return employee;
    }

    private static Order NewOrder(string userId, string orderId) =>
        OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            Id = orderId,
            UserId = userId,
            CustomerName = "Customer Jana",
            CustomerAddress = Address.Create("Dlouha 14", "Praha", "11000", "CZ"),
            // Completed, because a New/Confirmed/InProgress order BLOCKS the erasure outright.
            CurrentStatus = OrderStatus.Completed,
        });

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
