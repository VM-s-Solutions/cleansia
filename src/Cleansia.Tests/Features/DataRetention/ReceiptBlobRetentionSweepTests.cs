using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.DataRetention;

/// <summary>
/// Owner ruling 2026-09-28: a receipt's PDF is kept for the company's receipt retention period — 10 years by
/// default, pending the lawyer — and then deleted. The receipt row stays, marked, as the record of the
/// number, the sale and its fiscal registration. A PDF whose delete fails stays unmarked and is tried
/// again on the next run. Real repositories over SQLite, the <c>WorkContractAcceptanceMetadataSweepTests</c>
/// shape.
/// </summary>
public sealed class ReceiptBlobRetentionSweepTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Mock<IAppConfigurationProvider> _configProvider = new();
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();
    private readonly Mock<IBlobContainerClient> _receipts = new();
    private readonly List<string> _deleted = [];

    public ReceiptBlobRetentionSweepTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();

        _configProvider
            .Setup(c => c.GetTenantSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _blobClientFactory
            .Setup(f => f.GetBlobContainerClient(Core.AppServices.Common.Constants.BlobContainers.GeneratedReceipts))
            .Returns(_receipts.Object);
        _receipts
            .Setup(c => c.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((name, _) => _deleted.Add(name))
            .Returns(Task.CompletedTask);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void The_Period_Defaults_To_Ten_Years_Under_Its_Key()
    {
        Assert.Equal("retention.receipts.years", RetentionDefaults.ReceiptsYearsKey);
        Assert.Equal(10, RetentionDefaults.DefaultReceiptsYears);
    }

    /// <summary>
    /// The period runs from the end of the tax period of the supply — at the latest the end of its
    /// calendar year — not from the receipt's date: a receipt of 2 January ten years and more ago is
    /// still inside its period until that year has been over for ten years.
    /// </summary>
    [Fact]
    public async Task A_Receipt_Loses_Its_Pdf_Once_Its_Year_Has_Been_Over_For_The_Period_And_Keeps_Its_Row()
    {
        await EnsureSchemaAsync();
        var firstYearKept = DateTime.UtcNow.Year - RetentionDefaults.DefaultReceiptsYears;
        await SeedAsync(
            Receipt("rcpt-old", new DateTime(firstYearKept - 1, 6, 15, 12, 0, 0, DateTimeKind.Utc)),
            Receipt("rcpt-young", new DateTime(firstYearKept, 1, 2, 12, 0, 0, DateTimeKind.Utc)),
            Receipt("rcpt-new-year", new DateTime(firstYearKept - 1, 12, 31, 23, 30, 0, DateTimeKind.Utc)));

        await RunSweepAsync();

        Assert.Equal(["2016/rcpt-old.pdf"], _deleted);
        var rows = await ReadAllAsync();
        Assert.Equal(3, rows.Count);
        Assert.NotNull(rows.Single(r => r.Id == "rcpt-old").BlobDeletedAt);
        Assert.Equal("CZ-rcpt-old", rows.Single(r => r.Id == "rcpt-old").ReceiptNumber);
        Assert.Null(rows.Single(r => r.Id == "rcpt-young").BlobDeletedAt);
        Assert.Null(rows.Single(r => r.Id == "rcpt-new-year").BlobDeletedAt);
    }

    [Fact]
    public async Task A_Pdf_That_Would_Not_Delete_Stays_Unmarked()
    {
        await EnsureSchemaAsync();
        await SeedAsync(Receipt("rcpt-stuck", DateTime.UtcNow.AddYears(-12)));
        _receipts
            .Setup(c => c.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("storage unavailable"));

        await RunSweepAsync();

        Assert.Null(Assert.Single(await ReadAllAsync()).BlobDeletedAt);
    }

    [Fact]
    public async Task A_Pdf_Already_Deleted_Is_Not_Asked_For_Again()
    {
        await EnsureSchemaAsync();
        var receipt = Receipt("rcpt-done", DateTime.UtcNow.AddYears(-12));
        receipt.MarkBlobDeleted(DateTime.UtcNow.AddYears(-1));
        await SeedAsync(receipt);

        await RunSweepAsync();

        Assert.Empty(_deleted);
    }

    private static OrderReceipt Receipt(string id, DateTime issuedAt)
    {
        var receipt = OrderReceipt.Create($"order-{id}", $"CZ-{id}", $"{id}.pdf", $"2016/{id}.pdf", "language-en");
        receipt.Id = id;
        receipt.TenantId = TestTenants.Default;
        typeof(OrderReceipt).GetProperty(nameof(OrderReceipt.IssuedAt))!.SetValue(receipt, issuedAt);
        return receipt;
    }

    private CleansiaDbContext NewContext(ITenantProvider? tenantProvider = null) =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            tenantProvider ?? new FixedTenantProvider(TestTenants.Default));

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await TestTenants.EnsureCreatedWithRegistryAsync(ctx);
    }

    private async Task SeedAsync(params OrderReceipt[] rows)
    {
        await using var seed = NewContext();
        seed.OrderReceipts.AddRange(rows);
        await seed.CommitAsync(CancellationToken.None);
    }

    private async Task RunSweepAsync()
    {
        var tenantProvider = new FixedTenantProvider(null);
        await using var ctx = NewContext(tenantProvider);
        var session = new TestUserSessionProvider("system", "system@cleansia.test");
        var sweep = new DataRetentionBackgroundService(
            new UserRepository(ctx),
            new DeviceRepository(ctx, session),
            new GdprRequestRepository(ctx),
            new OrderRepository(ctx),
            new UserConsentRepository(ctx),
            new EmployeeDocumentRepository(ctx),
            new UserNotificationRepository(ctx),
            new CustomerActionAuditRepository(ctx),
            new DisputeRepository(ctx),
            new WorkContractAcceptanceRepository(ctx),
            new CleanerLegalDocumentAcceptanceRepository(ctx),
            new AddressRepository(ctx),
            new OrderPhotoRepository(ctx),
            new OrderReceiptRepository(ctx),
            new AdminActionAuditRepository(ctx),
            new EmployeeActionAuditRepository(ctx, Mock.Of<IServiceScopeFactory>()),
            new GuestOrderAccessTokenRepository(ctx),
            new TenantRepository(ctx),
            tenantProvider,
            _configProvider.Object,
            new DataRetentionConfig(new ConfigurationBuilder().Build()),
            _blobClientFactory.Object,
            new ArchiveWriteGate(),
            NullLogger<DataRetentionBackgroundService>.Instance);

        await sweep.RunAllRetentionTasksAsync(CancellationToken.None);
    }

    private async Task<List<OrderReceipt>> ReadAllAsync()
    {
        await using var ctx = NewContext();
        return await ctx.OrderReceipts.IgnoreQueryFilters().ToListAsync();
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
