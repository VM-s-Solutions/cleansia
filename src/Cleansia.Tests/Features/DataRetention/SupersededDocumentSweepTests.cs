using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using AppConstants = Cleansia.Core.AppServices.Common.Constants;

namespace Cleansia.Tests.Features.DataRetention;

/// <summary>
/// A replaced cleaner document goes a year after it was replaced: the blob, then the row. A blob that will
/// not delete keeps its row — the path is the only name the file has — and must not be read again in the
/// same run. Re-reading the head of the table did exactly that: the failing row came back on every batch,
/// the task spun until the host's timeout killed the whole retention run, and every task after it was
/// skipped for every company.
/// </summary>
public sealed class SupersededDocumentSweepTests : IDisposable
{
    private const string EmployeeId = "emp-superseded-docs";
    private const string FailingPath = "documents/emp-superseded-docs/v1_locked.pdf";

    private readonly SqliteConnection _connection;
    private readonly List<(string Template, IReadOnlyList<KeyValuePair<string, object?>> State)> _log = [];

    public SupersededDocumentSweepTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task A_Blob_That_Will_Not_Delete_Is_Tried_Once_Keeps_Its_Row_And_Is_Not_Counted()
    {
        await using (var seed = NewContext())
        {
            await TestTenants.EnsureCreatedWithRegistryAsync(seed);
            seed.AddRange(
                Replaced("documents/emp-superseded-docs/v1_passport.pdf"),
                Replaced(FailingPath),
                Replaced("documents/emp-superseded-docs/v1_contract.pdf"));
            await seed.CommitAsync(CancellationToken.None);
        }

        // Stops a run that keeps re-reading the failing row, so the failure shows as a count, not a hang.
        using var runaway = new CancellationTokenSource();
        var failingAttempts = 0;
        var blobClient = new Mock<IBlobContainerClient>();
        blobClient
            .Setup(c => c.DeleteAsync(FailingPath, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (++failingAttempts >= 3)
                {
                    runaway.Cancel();
                }
            })
            .ThrowsAsync(new InvalidOperationException("storage refused the delete"));

        await RunSweepAsync(blobClient.Object, runaway.Token);

        Assert.Equal(1, failingAttempts);
        await using var ctx = NewContext();
        var remaining = await ctx.Set<EmployeeDocument>().IgnoreQueryFilters().ToListAsync();
        Assert.Equal(FailingPath, Assert.Single(remaining).FilePath);
        var purgedPerCompany = _log
            .Where(e => e.Template.StartsWith("Purged", StringComparison.Ordinal))
            .Select(e => (int)e.State.Single(kv => kv.Key == "Total").Value!)
            .ToList();
        Assert.NotEmpty(purgedPerCompany);
        Assert.Equal(2, purgedPerCompany.Sum());
    }

    private static EmployeeDocument Replaced(string filePath)
    {
        var document = EmployeeDocument.Create(
            EmployeeId, Path.GetFileName(filePath), filePath, "application/pdf", 2048, new string('0', 64),
            DocumentType.IdentityCard, description: null, createdBy: "seed");
        document.Deactivated("seed", DateTimeOffset.UtcNow.AddDays(-400));
        return document;
    }

    private async Task RunSweepAsync(IBlobContainerClient documentBlobs, CancellationToken cancellationToken)
    {
        var tenantProvider = new FixedTenantProvider(null);
        await using var ctx = NewContext(tenantProvider);
        var session = new TestUserSessionProvider("system", "system@cleansia.test");
        var blobs = new Mock<IBlobContainerClientFactory>();
        blobs.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(Mock.Of<IBlobContainerClient>());
        blobs.Setup(f => f.GetBlobContainerClient(AppConstants.BlobContainers.EmployeeDocuments)).Returns(documentBlobs);

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
            new AppConfigurationProvider(ctx),
            new DataRetentionConfig(new ConfigurationBuilder().Build()),
            blobs.Object,
            new ArchiveWriteGate(),
            new CapturingLogger(_log));

        await sweep.RunAllRetentionTasksAsync(cancellationToken);
    }

    private CleansiaDbContext NewContext(ITenantProvider? tenantProvider = null) =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            tenantProvider ?? new FixedTenantProvider(TestTenants.Default));

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }

    private sealed class CapturingLogger(List<(string Template, IReadOnlyList<KeyValuePair<string, object?>> State)> entries)
        : ILogger<DataRetentionBackgroundService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (state is IReadOnlyList<KeyValuePair<string, object?>> values)
            {
                var template = values.FirstOrDefault(kv => kv.Key == "{OriginalFormat}").Value as string ?? string.Empty;
                entries.Add((template, values));
            }
        }
    }
}
