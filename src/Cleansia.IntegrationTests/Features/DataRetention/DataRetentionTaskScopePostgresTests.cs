using System.Text.Json;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Domain.Users;
using Cleansia.Functions.Core;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.DataRetention;

[Collection("PostgresCollection")]
public sealed class DataRetentionTaskScopePostgresTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    [Fact]
    public async Task A_Refused_Pii_Batch_Does_Not_Hold_Back_Later_Tasks_Or_The_Frozen_Company()
    {
        var scopes = new List<ObservedScope>();
        var fault = new RefusePiiSave(Fixture.GetConnectionString());
        await TestMethod<bool>(
            setup: services => Register(services, scopes, fault),
            arrange: SeedAsync,
            act: async provider =>
            {
                await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync();
                await using (var check = provider.CreateAsyncScope())
                {
                    var context = check.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                    await AssertRowsAsync(context, "refused-pii", fault.Attempts);
                }
                Assert.Equal(31, scopes.Count);
                var children = scopes.Skip(1).ToList();
                Assert.Equal(30, children.Select(s => s.Context).Distinct().Count());
                Assert.Equal(30, children.Select(s => s.Gate).Distinct().Count());
                Assert.Equal(30, children.Select(s => s.Provider).Distinct().Count());
                Assert.Equal(30, children.Select(s => s.Settings).Distinct().Count());
                Assert.Equal(15, children.Count(s => s.TenantAtCreation == TestTenants.Default));
                Assert.Equal(15, children.Count(s => s.TenantAtCreation == TestTenants.Second));
                Assert.All(children, s =>
                {
                    Assert.False(s.GateOpenAtCreation);
                    Assert.False(s.Gate.IsOpen);
                    Assert.All(s.Reads, r => { Assert.True(r.GateOpen); Assert.Equal(s.TenantAtCreation, r.Tenant); });
                    Assert.Throws<ObjectDisposedException>(() => s.Context.ChangeTracker.Entries().ToList());
                });
                Console.WriteLine(JsonSerializer.Serialize(new { phase = "retention-child-identities", children = children.Select(s => new
                {
                    s.Id, s.TenantAtCreation, s.GateOpenAtCreation, gateClosedAfterTask = !s.Gate.IsOpen, s.Reads,
                    contextIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s.Context),
                    gateIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s.Gate),
                    providerIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s.Provider),
                    settingsIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s.Settings!)
                }) }));

                await using (var ordinary = provider.CreateAsyncScope())
                {
                    ordinary.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Second);
                    var context = ordinary.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                    var gate = ordinary.ServiceProvider.GetRequiredService<IArchiveWriteGate>();
                    Assert.False(gate.IsOpen);
                    (await context.Orders.SingleAsync(o => o.Id == "retention-b-old")).UpdatePhone("ordinary write refused");
                    await Assert.ThrowsAsync<CompanyArchivedException>(() => context.CommitAsync(CancellationToken.None));
                }
                await using (var repeat = provider.CreateAsyncScope())
                {
                    await repeat.ServiceProvider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync();
                    await using var check = provider.CreateAsyncScope();
                    await AssertRowsAsync(check.ServiceProvider.GetRequiredService<CleansiaDbContext>(), "repeated-refused-pii", fault.Attempts);
                    Assert.True(fault.Attempts >= 2);
                }
                fault.Enabled = false;
                await using var retry = provider.CreateAsyncScope();
                await retry.ServiceProvider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync();
                return true;
            },
            assert: async (context, _) =>
            {
                var orders = await context.Orders.IgnoreQueryFilters().AsNoTracking().OrderBy(o => o.Id).ToListAsync();
                Console.WriteLine(JsonSerializer.Serialize(new { phase = "cleared-fault-fresh-retry", orders = orders.Select(o => new { o.Id, o.TenantId, o.CustomerName, o.PaymentStatus }) }));
                Assert.Equal(AnonymizationMarker.Value, orders.Single(o => o.Id == "retention-a-old").CustomerName);
                Assert.All(scopes, s => Assert.False(s.Gate.IsOpen));
            }, transactional: false);
    }

    [Fact]
    public async Task Host_Stop_During_Blob_Delete_Does_Not_Delete_The_Next_Blob_Or_Start_Another_Task()
    {
        using var stop = new CancellationTokenSource();
        var scopes = new List<ObservedScope>();
        var deletes = new List<string>();
        var client = new Mock<IBlobContainerClient>();
        client.Setup(c => c.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((name, ct) =>
            {
                deletes.Add(name);
                stop.Cancel();
                return Task.FromCanceled(ct);
            });
        await TestMethod<bool>(
            setup: services => Register(services, scopes, receiptBlobs: client.Object),
            arrange: async context =>
            {
                await SeedAsync(context);
                var languageId = await context.Languages.Select(l => l.Id).FirstAsync();
                foreach (var id in new[] { "retention-a-old", "retention-a-young" })
                {
                    var receipt = OrderReceipt.Create(id, $"CZ-{id}", $"{id}.pdf", $"old/{id}.pdf", languageId);
                    receipt.TenantId = TestTenants.Default;
                    typeof(OrderReceipt).GetProperty(nameof(OrderReceipt.IssuedAt))!.SetValue(receipt, DateTime.UtcNow.AddYears(-12));
                    context.OrderReceipts.Add(receipt);
                }
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var failure = await Record.ExceptionAsync(() => provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(stop.Token));
                await using var check = provider.CreateAsyncScope();
                var receipts = await check.ServiceProvider.GetRequiredService<CleansiaDbContext>().OrderReceipts.IgnoreQueryFilters().AsNoTracking().ToListAsync();
                Console.WriteLine(JsonSerializer.Serialize(new { phase = "retention-blob-host-stop", error = failure?.GetType().Name, deletes,
                    receipts = receipts.Select(r => new { r.OrderId, r.BlobDeletedAt }),
                    scopes = scopes.Select(s => new { s.Id, s.TenantAtCreation, closed = !s.Gate.IsOpen }) }));
                Assert.IsAssignableFrom<OperationCanceledException>(failure);
                Assert.Single(deletes);
                Assert.Equal(13, scopes.Count);
                Assert.All(scopes, s => Assert.False(s.Gate.IsOpen));
                Assert.DoesNotContain(scopes, s => s.TenantAtCreation == TestTenants.Second);
                return true;
            },
            assert: async (context, _) => Assert.All(await context.OrderReceipts.IgnoreQueryFilters().AsNoTracking().ToListAsync(), r => Assert.Null(r.BlobDeletedAt)),
            transactional: false);
    }

    [Fact]
    public async Task Host_Stop_Closes_The_Active_Task_Gate_And_Stops_Before_Another_Task()
    {
        using var stop = new CancellationTokenSource();
        var scopes = new List<ObservedScope>();
        await TestMethod<bool>(
            setup: services => Register(services, scopes, cancel: stop),
            arrange: SeedAsync,
            act: async provider =>
            {
                var failure = await Record.ExceptionAsync(() => provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(stop.Token));
                Console.WriteLine(JsonSerializer.Serialize(new { phase = "retention-host-stop", error = failure?.GetType().Name,
                    scopes = scopes.Select(s => new { s.Id, s.TenantAtCreation, closed = !s.Gate.IsOpen, s.Reads }) }));
                Assert.IsAssignableFrom<OperationCanceledException>(failure);
                Assert.Equal(5, scopes.Count);
                Assert.All(scopes, s => Assert.False(s.Gate.IsOpen));
                Assert.DoesNotContain(scopes, s => s.TenantAtCreation == TestTenants.Second);
                return true;
            },
            assert: async (context, _) =>
            {
                var orders = await context.Orders.IgnoreQueryFilters().AsNoTracking().ToListAsync();
                Assert.All(orders, o => Assert.NotEqual(AnonymizationMarker.Value, o.CustomerName));
            }, transactional: false);
    }

    private static Task Register(IServiceCollection services, List<ObservedScope> scopes, RefusePiiSave? fault = null, CancellationTokenSource? cancel = null,
        IBlobContainerClient? receiptBlobs = null)
    {
        services.AddFunctionsProcessing();
        services.Replace(ServiceDescriptor.Scoped<ITenantProvider>(sp => new TenantProvider(sp.GetRequiredService<IHttpContextAccessor>())));
        services.AddScoped(sp => new ObservedScope(sp.GetRequiredService<CleansiaDbContext>(), sp.GetRequiredService<IArchiveWriteGate>(), sp.GetRequiredService<ITenantProvider>()));
        services.Replace(ServiceDescriptor.Scoped<IAppConfigurationProvider>(sp =>
        {
            var observation = sp.GetRequiredService<ObservedScope>();
            var settings = new ObservedSettings(new AppConfigurationProvider(observation.Context), observation, cancel);
            observation.Settings = settings;
            return settings;
        }));
        services.AddScoped<DataRetentionBackgroundService>(sp =>
        {
            var observation = sp.GetRequiredService<ObservedScope>();
            var result = ActivatorUtilities.CreateInstance<DataRetentionBackgroundService>(sp);
            scopes.Add(observation);
            return result;
        });
        services.Replace(ServiceDescriptor.Scoped<IDataRetentionBackgroundService>(sp => sp.GetRequiredService<DataRetentionBackgroundService>()));
        if (fault is not null) services.AddDbContext<CleansiaDbContext>((_, options) => options.AddInterceptors(fault));
        var blob = new Mock<IBlobContainerClientFactory>();
        blob.Setup(b => b.GetBlobContainerClient(It.IsAny<string>())).Returns(Mock.Of<IBlobContainerClient>());
        if (receiptBlobs is not null) blob.Setup(b => b.GetBlobContainerClient(Cleansia.Core.AppServices.Common.Constants.BlobContainers.GeneratedReceipts)).Returns(receiptBlobs);
        services.Replace(ServiceDescriptor.Singleton(blob.Object));
        return Task.CompletedTask;
    }

    private static async Task AssertRowsAsync(CleansiaDbContext context, string phase, int attempts)
    {
        var orders = await context.Orders.IgnoreQueryFilters().AsNoTracking().OrderBy(o => o.Id).ToListAsync();
        var disputes = await context.Disputes.IgnoreQueryFilters().AsNoTracking().OrderBy(d => d.Id).ToListAsync();
        var notices = await context.Set<UserNotification>().IgnoreQueryFilters().AsNoTracking().ToListAsync();
        Console.WriteLine(JsonSerializer.Serialize(new { phase, attempts,
            orders = orders.Select(o => new { o.Id, o.TenantId, o.CustomerName, o.PaymentStatus }),
            disputes = disputes.Select(d => new { d.TenantId, d.Description, d.TextRetainedUntil }),
            notices = notices.Select(n => new { n.TenantId, n.UserId }) }));
        Assert.Equal("retained customer", orders.Single(o => o.Id == "retention-a-old").CustomerName);
        Assert.Equal(AnonymizationMarker.Value, orders.Single(o => o.Id == "retention-b-old").CustomerName);
        Assert.Equal("retained customer", orders.Single(o => o.Id == "retention-b-mid").CustomerName);
        Assert.All(disputes, d => { Assert.Equal(AnonymizationMarker.Value, d.Description); Assert.Null(d.TextRetainedUntil); });
        Assert.Equal(TestTenants.Second, Assert.Single(notices).TenantId);
        Assert.True(attempts >= 1);
    }

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true); country.Id = "retention-country";
        var currency = Currency.Create("CZK", "Kč", "Czech koruna"); currency.Id = "retention-currency";
        context.AddRange(country, currency, Language.Create("en", "English"));
        foreach (var (id, tenant, years, days) in new[] { ("a", TestTenants.Default, "1", "30"), ("b", TestTenants.Second, "4", "90") })
        {
            var user = User.CreateWithPassword($"{id}@retention.test", "Test-password-1!", id, "Customer", UserProfile.Customer);
            user.Id = $"retention-user-{id}"; user.TenantId = tenant; context.Add(user);
            var pii = TenantConfiguration.Create(RetentionDefaults.OrderPiiYearsKey, years); pii.TenantId = tenant; context.Add(pii);
            var notifications = TenantConfiguration.Create(RetentionDefaults.NotificationsDaysKey, days); notifications.TenantId = tenant; context.Add(notifications);
            var notice = UserNotification.Create(user.Id, "order.completed", "{}", tenant);
            notice.Created("seed", DateTimeOffset.UtcNow.AddDays(-60)); context.Add(notice);
            var order = NewOrder($"retention-{id}-old", tenant, user.Id, id == "a" ? 3 : 5);
            context.Add(order);
            // A newer order keeps the source address in the real shared-address census. This fixture
            // isolates the stale order token from the separate FK refusal when deleting an address.
            context.Add(NewOrder($"retention-{id}-" + (id == "a" ? "young" : "mid"), tenant, user.Id,
                id == "a" ? 0 : 3, order.CustomerAddress));
            var dispute = new Dispute(order.Id, user.Id, DisputeReason.QualityIssue, "old dispute text", user.Id);
            dispute.TenantId = tenant; dispute.RetainTextUntil(DateTimeOffset.UtcNow.AddDays(-1)); context.Add(dispute);
        }
        await context.CommitAsync(CancellationToken.None);
        var frozen = await context.Tenants.SingleAsync(t => t.Id == TestTenants.Second);
        frozen.RequestWindDown(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)), "admin", DateTimeOffset.UtcNow.AddDays(-60));
        frozen.Deactivate("admin", DateTimeOffset.UtcNow.AddDays(-45));
        frozen.RequestArchive("admin", DateTimeOffset.UtcNow.AddDays(-1));
        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOrder(string id, string tenant, string userId, int years, Address? sharedAddress = null)
    {
        var address = sharedAddress ?? Address.Create("Private address", "Prague", "11000", "retention-country"); address.TenantId = tenant;
        var order = Order.Create("retained customer", "customer@retention.test", "+420777444556", address, 2, 1,
            DateTime.UtcNow.AddYears(-years), PaymentType.Card, 1200m, "retention-currency", PaymentStatus.Paid,
            BookingPolicy.CancellationTermsAtBooking, userId: userId);
        order.Id = id; order.TenantId = tenant; order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        foreach (var status in order.OrderStatusHistory) status.TenantId = tenant;
        return order;
    }

    private sealed class RefusePiiSave(string connectionString) : SaveChangesInterceptor
    {
        public bool Enabled { get; set; } = true;
        public int Attempts { get; private set; }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Enabled || !eventData.Context!.ChangeTracker.Entries<Order>().Any(o => o.Entity.Id == "retention-a-old"
                    && o.State == EntityState.Modified && o.Entity.CustomerName == AnonymizationMarker.Value)) return result;
            Attempts++;
            await using var competing = new CleansiaDbContext(new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(connectionString).Options);
            var order = await competing.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == "retention-a-old", cancellationToken);
            order.UpdatePaymentStatus(Attempts % 2 == 0 ? PaymentStatus.PartiallyRefunded : PaymentStatus.Refunded);
            await competing.SaveChangesAsync(cancellationToken);
            return result;
        }
    }
    private sealed class ObservedScope(CleansiaDbContext context, IArchiveWriteGate gate, ITenantProvider provider)
    {
        public Guid Id { get; } = Guid.NewGuid();
        public CleansiaDbContext Context { get; } = context;
        public IArchiveWriteGate Gate { get; } = gate;
        public ITenantProvider Provider { get; } = provider;
        public object? Settings { get; set; }
        public string? TenantAtCreation { get; } = provider.GetCurrentTenantId();
        public bool GateOpenAtCreation { get; } = gate.IsOpen;
        public List<SettingRead> Reads { get; } = [];
    }
    private sealed record SettingRead(string Key, string? Tenant, bool GateOpen);
    private sealed class ObservedSettings(IAppConfigurationProvider inner, ObservedScope observation, CancellationTokenSource? cancel) : IAppConfigurationProvider
    {
        public async Task<string?> GetTenantSettingAsync(string key, CancellationToken ct = default)
        {
            observation.Reads.Add(new SettingRead(key, observation.Provider.GetCurrentTenantId(), observation.Gate.IsOpen));
            if (cancel is not null && key == RetentionDefaults.OrderPiiYearsKey && observation.Provider.GetCurrentTenantId() == TestTenants.Default)
            { cancel.Cancel(); ct.ThrowIfCancellationRequested(); }
            return await inner.GetTenantSettingAsync(key, ct);
        }
        public Task<string?> GetTenantSettingAsync(string tenantId, string key, CancellationToken ct = default) => inner.GetTenantSettingAsync(tenantId, key, ct);
        public Task<CountryConfiguration?> GetCountryConfigurationAsync(string countryId, CancellationToken ct = default) => inner.GetCountryConfigurationAsync(countryId, ct);
    }
}
