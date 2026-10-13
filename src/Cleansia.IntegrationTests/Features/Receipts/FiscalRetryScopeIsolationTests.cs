using System.Data.Common;
using Microsoft.Extensions.Logging;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Fiscal.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Respawn;
using Xunit.Abstractions;

namespace Cleansia.IntegrationTests.Features.Receipts;

[Collection("PostgresCollection")]
public sealed class FiscalRetryScopeIsolationTests(PostgresContainerFixture fixture, ITestOutputHelper output) : BaseIntegrationTest(fixture)
{
    private const string ReceiptB = "receipt-fiscal-b";
    private readonly Dictionary<string, FiscalResult> _authorityLedger = [];
    private readonly List<string> _sent = [];
    private readonly List<(string Token, string Tenant, string Company, string Country)> _registrations = [];

    private CleansiaDbContext NewContext(ITenantProvider? tenant = null, params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(Fixture.GetConnectionString()).AddInterceptors(interceptors).Options,
        new TestUserSessionProvider("system", "system@cleansia.test"), tenant ?? new ScopeTenantProvider());

    private async Task SeedAsync(FiscalEnforcementMode mode = FiscalEnforcementMode.BlockingOnline, bool feeC = false)
    {
        await using var connection = new NpgsqlConnection(Fixture.GetConnectionString());
        await connection.OpenAsync();
        var reset = await Respawner.CreateAsync(connection, new RespawnerOptions
        { DbAdapter = DbAdapter.Postgres, SchemasToExclude = ["pg_catalog", "information_schema"] });
        await reset.ResetAsync(connection);
        await SeedTenantRegistryAsync(connection);
        await using (var catalog = NewContext())
        {
            var language = Language.Create("en", "English");
            language.Id = "lang-fiscal";
            catalog.Languages.Add(language);
            var currency = Currency.Create("CZK", "CZK", "CZK");
            currency.Id = "currency-fiscal";
            catalog.Currencies.Add(currency);
            foreach (var (id, iso, tenant) in new[] { ("country-fiscal-cz", "CZ", TestTenants.Default), ("country-fiscal-sk", "SK", TestTenants.Second) })
            {
                var country = Country.Create(iso, iso == "CZ" ? "CZE" : "SVK", iso, isServiced: true);
                country.Id = id;
                catalog.Countries.Add(country);
                catalog.CountryConfigurations.Add(CountryConfiguration.Create(id, "CZK", "en", 0.21m)
                    .AssignOperator(tenant).UpdateFiscalEnforcementMode(mode));
            }
            await catalog.CommitAsync(CancellationToken.None);
        }
        foreach (var letter in new[] { "a", "b", "c" })
        {
            var tenant = letter == "c" ? TestTenants.Second : TestTenants.Default;
            var country = letter == "c" ? "country-fiscal-sk" : "country-fiscal-cz";
            var provider = new ScopeTenantProvider();
            provider.SetTenantOverride(tenant);
            await using var ctx = NewContext(provider);
            if (letter != "b") ctx.CompanyInfo.Add(CompanyInfo.Create(tenant + " legal", tenant, "12345678", "Test St 1", "Brno", "60200", country));
            User? feeOwner = null;
            if (feeC && letter == "c")
            {
                feeOwner = User.CreateWithPassword("fee-fiscal@cleansia.test", "Password123!", "Fee", "Customer");
                ctx.Users.Add(feeOwner);
            }
            var order = Order.Create("Fiscal Customer", letter + "@cleansia.test", "+420000000000",
                Address.Create("Test St 1", "Brno", "60200", country), 1, 1, DateTime.UtcNow.AddDays(1),
                PaymentType.Card, 1000m, "currency-fiscal", PaymentStatus.Paid,
                userId: feeOwner?.Id, cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            order.Id = "order-fiscal-" + letter;
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
            ctx.Orders.Add(order);
            OrderReceipt receipt;
            if (feeC && letter == "c")
            {
                var receivable = Receivable.ForCashCancellationFee(order, 100m);
                ctx.Receivables.Add(receivable);
                receipt = OrderReceipt.CreateFee(receivable, "R-FISCAL-" + letter, letter + ".pdf", letter + ".pdf", "lang-fiscal");
            }
            else receipt = OrderReceipt.Create(order.Id, "R-FISCAL-" + letter, letter + ".pdf", letter + ".pdf", "lang-fiscal");
            receipt.Id = "receipt-fiscal-" + letter;
            receipt.ScheduleImmediateFiscalRetry();
            ctx.OrderReceipts.Add(receipt);
            await ctx.CommitAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task A_Refused_Fiscal_State_Commit_Owns_A_Different_Context_From_Its_Neighbors()
    {
        await SeedAsync();
        var run = await RunAsync(new RefuseBCommit(1));
        await AssertBoundaryAsync(1);
        output.WriteLine($"contexts={run.Contexts.Count}; distinct={run.Contexts.Distinct().Count()}; attempted={run.Processed}");
        Assert.Equal(4, run.Contexts.Distinct().Count());
        Assert.Equal(run.Contexts.OrderBy(id => id), run.Disposed.Distinct().OrderBy(id => id));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task The_Actual_Fiscal_Claim_And_Metadata_Commit_Faults_Leave_Only_Permitted_State(int boundary)
    {
        await SeedAsync();
        var fault = new RefuseBCommit(boundary);
        var first = await RunAsync(fault);
        Assert.Equal(3, first.Processed);
        Assert.Equal(1, fault.Count);
        await AssertBoundaryAsync(boundary);
        var secondFault = new RefuseBCommit(boundary);
        var second = await RunAsync(secondFault);
        Assert.Equal(boundary == 1 ? 1 : 0, second.Processed);
        Assert.Equal(boundary == 1 ? 1 : 0, secondFault.Count);
        await AssertBoundaryAsync(boundary);
        Assert.Equal(3, _authorityLedger.Count);
        Assert.All(_registrations, r => Assert.Equal(r.Tenant + " legal", r.Company));
        Assert.Equal(boundary == 3 ? 1 : 0, _sent.Count(id => id == "order-fiscal-b"));
    }

    [Theory]
    [InlineData(FiscalEnforcementMode.None, false)]
    [InlineData(FiscalEnforcementMode.AsyncBackground, false)]
    [InlineData(FiscalEnforcementMode.BlockingOnline, true)]
    [InlineData(FiscalEnforcementMode.BlockingWithOfflineCache, true)]
    public async Task Two_Companies_Keep_Their_Mode_And_Fee_Receipt_Releases_No_Order_Email(FiscalEnforcementMode mode, bool releases)
    {
        await SeedAsync(mode, feeC: true);
        Assert.Equal(3, (await RunAsync()).Processed);
        await using var read = NewContext();
        var rows = await read.OrderReceipts.IgnoreQueryFilters().AsNoTracking().Include(r => r.Language).Include(r => r.Receivable).ToListAsync();
        Assert.All(rows, r => Assert.NotNull(r.FiscalCode));
        Assert.All(rows, r => Assert.Equal("en", r.Language!.Code));
        var fee = Assert.Single(rows, r => r.IsFee);
        Assert.NotNull(fee.Receivable);
        Assert.False(fee.EmailSent);
        Assert.Equal(releases ? 2 : 0, _sent.Count);
        Assert.DoesNotContain("order-fiscal-c", _sent);
        Assert.Equal(releases ? 2 : 0, await read.GuestOrderAccessTokens.IgnoreQueryFilters().CountAsync());
        Assert.Equal(3, _authorityLedger.Count);
        Assert.All(_registrations, r => Assert.Equal(r.Tenant + " legal", r.Company));
    }

    [Fact]
    public async Task Host_Cancellation_Stops_Before_Any_Neighbor_Effect()
    {
        await SeedAsync();
        using var cancellation = new CancellationTokenSource();
        var fault = new CancelAtFirstSave(cancellation);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(fault, cancellation.Token));
        Assert.Single(_authorityLedger);
        Assert.Empty(_sent);
        await using var read = NewContext();
        Assert.All(await read.OrderReceipts.IgnoreQueryFilters().ToListAsync(), r => Assert.Null(r.FiscalCode));
        Assert.Empty(await read.GuestOrderAccessTokens.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task A_Receipt_Whose_Order_Belongs_To_Another_Operator_Is_Skipped()
    {
        await SeedAsync();
        await using (var move = NewContext())
            await move.Orders.IgnoreQueryFilters().Where(o => o.Id == "order-fiscal-b")
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.TenantId, TestTenants.Second));
        Assert.Equal(2, (await RunAsync()).Processed);
        Assert.DoesNotContain(_registrations, r => r.Token == "R-FISCAL-b");
        await using var read = NewContext();
        var b = await read.OrderReceipts.IgnoreQueryFilters().SingleAsync(r => r.Id == ReceiptB);
        Assert.Null(b.FiscalCode);
        Assert.False(b.EmailSent);
        Assert.DoesNotContain("order-fiscal-b", _sent);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Selected_Receipt_Registered_Or_Moved_Later_Is_Rechecked_In_Its_Child(bool registered)
    {
        await SeedAsync();
        var race = new AfterDueRead(async () =>
        {
            await using var update = NewContext();
            if (registered)
            {
                var b = await update.OrderReceipts.IgnoreQueryFilters().SingleAsync(r => r.Id == ReceiptB);
                b.SetFiscalData("existing", "already-signed", DateTime.UtcNow);
                await update.CommitAsync(CancellationToken.None);
            }
            else await update.OrderReceipts.IgnoreQueryFilters().Where(r => r.Id == ReceiptB)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.FiscalNextRetryAt, DateTime.UtcNow.AddHours(1)));
        });
        Assert.Equal(2, (await RunAsync(race)).Processed);
        Assert.True(race.Ran);
        Assert.DoesNotContain(_registrations, r => r.Token == "R-FISCAL-b");
        Assert.DoesNotContain("order-fiscal-b", _sent);
        await using var read = NewContext();
        var b = await read.OrderReceipts.IgnoreQueryFilters().SingleAsync(r => r.Id == ReceiptB);
        Assert.Equal(registered ? "already-signed" : null, b.FiscalCode);
        Assert.False(b.EmailSent);
        Assert.Empty(await read.GuestOrderAccessTokens.IgnoreQueryFilters().Where(t => t.OrderId == b.OrderId).ToListAsync());
    }

    private async Task AssertBoundaryAsync(int boundary)
    {
        await using var read = NewContext();
        var rows = await read.OrderReceipts.IgnoreQueryFilters().AsNoTracking().OrderBy(r => r.Id).ToListAsync();
        output.WriteLine($"boundary={boundary}; receipts={string.Join(';', rows.Select(r => $"{r.Id}:{r.TenantId}:fiscal={r.FiscalCode}:email={r.EmailSent}:metadata={r.EmailMessageId}"))}; tokens={await read.GuestOrderAccessTokens.IgnoreQueryFilters().CountAsync()}; sent={string.Join(',', _sent)}; authority entries={_authorityLedger.Count}");
        foreach (var row in rows.Where(r => r.Id != ReceiptB))
        {
            Assert.NotNull(row.FiscalCode);
            Assert.True(row.EmailSent);
            Assert.NotNull(row.EmailMessageId);
            Assert.Single(await read.GuestOrderAccessTokens.IgnoreQueryFilters().Where(t => t.OrderId == row.OrderId).ToListAsync());
        }
        var b = Assert.Single(rows, r => r.Id == ReceiptB);
        Assert.Equal(boundary != 1, b.FiscalCode is not null);
        Assert.Equal(boundary == 3, b.EmailSent);
        Assert.Null(b.EmailMessageId);
        Assert.Equal(boundary == 3 ? 1 : 0, await read.GuestOrderAccessTokens.IgnoreQueryFilters().CountAsync(t => t.OrderId == b.OrderId));
        Assert.Equal(boundary == 3 ? 1 : 0, _sent.Count(id => id == b.OrderId));
    }

    private async Task<(int Processed, List<Guid> Contexts, List<Guid> Disposed)> RunAsync(IInterceptor? fault = null, CancellationToken cancellationToken = default)
    {
        var contexts = new List<Guid>();
        var disposed = new List<Guid>();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddScoped<ITenantProvider>(_ => new ScopeTenantProvider());
        services.AddScoped<IUserSessionProvider>(_ => new TestUserSessionProvider("system", "system@cleansia.test"));
        services.AddScoped(sp =>
        {
            var ctx = new DisposalRecordingContext(new DbContextOptionsBuilder<CleansiaDbContext>()
                .UseNpgsql(Fixture.GetConnectionString()).AddInterceptors(fault is null ? [] : [fault]).Options,
                sp.GetRequiredService<ITenantProvider>(), disposed);
            contexts.Add(ctx.ContextId.InstanceId);
            return (CleansiaDbContext)ctx;
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CleansiaDbContext>());
        services.AddScoped<IOrderReceiptRepository, OrderReceiptRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICountryConfigurationRepository, CountryConfigurationRepository>();
        services.AddScoped<ICompanyInfoRepository, CompanyInfoRepository>();
        services.AddScoped<ICountryRepository, CountryRepository>();
        services.AddScoped<ILanguageRepository, LanguageRepository>();
        services.AddScoped<IFiscalCounterRepository, FiscalCounterRepository>();
        services.AddScoped<IGuestOrderAccessTokenRepository, GuestOrderAccessTokenRepository>();
        services.AddScoped<GuestOrderAccessTokenIssuer>();
        services.AddScoped<IReceiptService, ReceiptService>();
        var pdf = new Mock<IPdfService>();
        pdf.Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>())).Returns([1, 2, 3]);
        services.AddSingleton(pdf.Object);
        services.AddScoped<IBlobContainerClientFactory>(_ =>
        {
            var blob = new Mock<IBlobContainerClient>();
            blob.Setup(b => b.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new BlobFile(new MemoryStream([1, 2, 3]), "application/pdf"));
            var factory = new Mock<IBlobContainerClientFactory>();
            factory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(blob.Object);
            return factory.Object;
        });
        services.AddScoped<IFiscalServiceResolver>(sp =>
        {
            var tenant = sp.GetRequiredService<ITenantProvider>();
            var fiscal = new Mock<IFiscalService>();
            fiscal.SetupGet(f => f.ProviderKey).Returns("test-fiscal");
            fiscal.SetupGet(f => f.RegisterIsIdempotent).Returns(true);
            fiscal.Setup(f => f.RegisterReceiptAsync(It.IsAny<FiscalReceiptRequest>(), It.IsAny<CancellationToken>()))
                .Returns((FiscalReceiptRequest request, CancellationToken ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    _registrations.Add((request.IdempotencyKey, tenant.GetCurrentTenantId()!, request.CompanyLegalName, request.CountryCode));
                    Assert.Equal(request.ReceiptNumber, request.IdempotencyKey);
                    if (!_authorityLedger.TryGetValue(request.IdempotencyKey, out var result))
                        _authorityLedger.Add(request.IdempotencyKey, result = FiscalResult.Success("SIG-" + request.IdempotencyKey, string.Empty));
                    return Task.FromResult(result);
                });
            var resolver = new Mock<IFiscalServiceResolver>();
            resolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(fiscal.Object);
            return resolver.Object;
        });
        services.AddScoped<IEmailService>(_ =>
        {
            var email = new Mock<IEmailService>();
            email.Setup(e => e.SendOrderReceiptEmailAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
                .Returns(async (string address, Order order, byte[]? pdfBytes, string name, string language, CancellationToken ct, string? token) =>
                {
                    ct.ThrowIfCancellationRequested();
                    await using var verify = NewContext();
                    Assert.True((await verify.OrderReceipts.IgnoreQueryFilters().SingleAsync(r => r.OrderId == order.Id && r.ReceivableId == null)).EmailSent);
                    Assert.Single(await verify.GuestOrderAccessTokens.IgnoreQueryFilters().Where(t => t.OrderId == order.Id).ToListAsync());
                    Assert.NotNull(token);
                    _sent.Add(order.Id);
                    return "msg-" + order.Id;
                });
            return email.Object;
        });
        services.AddScoped<FiscalRetryService>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var processed = await scope.ServiceProvider.GetRequiredService<FiscalRetryService>().ProcessDueRetriesAsync(cancellationToken);
        return (processed, contexts, disposed);
    }

    private sealed class DisposalRecordingContext(DbContextOptions<CleansiaDbContext> options,
        ITenantProvider tenant, List<Guid> disposed) : CleansiaDbContext(options,
            new TestUserSessionProvider("system", "system@cleansia.test"), tenant)
    {
        public override async ValueTask DisposeAsync()
        {
            var id = ContextId.InstanceId;
            await base.DisposeAsync();
            disposed.Add(id);
        }
    }

    private sealed class AfterDueRead(Func<Task> change) : DbCommandInterceptor
    {
        public bool Ran { get; private set; }
        public override async ValueTask<InterceptionResult> DataReaderClosingAsync(DbCommand command,
            DataReaderClosingEventData data, InterceptionResult result)
        {
            if (!Ran && command.CommandText.Contains("FiscalNextRetryAt", StringComparison.Ordinal)
                && command.CommandText.Contains("LIMIT", StringComparison.Ordinal))
            { Ran = true; await change(); }
            return result;
        }
    }

    private sealed class RefuseBCommit(int boundary) : SaveChangesInterceptor
    {
        public int Count { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            var b = data.Context!.ChangeTracker.Entries<OrderReceipt>().FirstOrDefault(e => e.Entity.Id == ReceiptB && e.State == EntityState.Modified);
            if (b is not null && (boundary == 1 && b.Entity.FiscalCode != null && b.Property(r => r.FiscalCode).OriginalValue == null
                || boundary == 2 && b.Entity.EmailSent && !b.Property(r => r.EmailSent).OriginalValue
                || boundary == 3 && b.Entity.EmailMessageId != null && b.Property(r => r.EmailMessageId).OriginalValue == null))
            { Count++; throw new InvalidOperationException($"Refused B actual commit {boundary}"); }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CancelAtFirstSave(CancellationTokenSource cancellation) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        { cancellation.Cancel(); ct.ThrowIfCancellationRequested(); return ValueTask.FromResult(result); }
    }

    private sealed class ScopeTenantProvider : ITenantProvider
    {
        private string? _tenant;
        public string? GetCurrentTenantId() => _tenant;
        public void SetTenantOverride(string tenant) => _tenant = tenant;
        public void ClearTenantOverride() => _tenant = null;
    }
}
