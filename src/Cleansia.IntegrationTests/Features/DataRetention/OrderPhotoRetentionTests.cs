using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using TestTenants = Cleansia.TestUtilities.TestTenants;

namespace Cleansia.IntegrationTests.Features.DataRetention;

/// <summary>
/// Order photos are kept seven days after the job is completed (owner ruling 2026-09-22), then the blob and
/// the row go — through the REAL sweep on real Postgres. An order whose dispute is still open keeps its
/// photos because they are the dispute's evidence; a closed dispute does not hold them. An order that was
/// never completed is outside the rule. The blob is deleted by its container-relative name, and a blob that
/// will not delete keeps its row (the URL is the only name the blob has) without stalling the run. A
/// backlog wider than one batch drains in one run.
/// </summary>
[Collection("PostgresCollection")]
public class OrderPhotoRetentionTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-photo-ret";
    private const string CurrencyId = "currency-czk-photo-ret";
    private const string CleanerUserId = "user-cln-photo-ret";
    private const string CleanerId = "emp-cln-photo-ret";

    private const string PastWindowOrderId = "order-photo-ret-past";
    private const string InsideWindowOrderId = "order-photo-ret-inside";
    private const string NeverCompletedOrderId = "order-photo-ret-never";
    private const string OpenDisputeOrderId = "order-photo-ret-open";
    private const string ClosedDisputeOrderId = "order-photo-ret-closed";
    private const string BlobFailsOrderId = "order-photo-ret-blob-fails";

    [Fact]
    public async Task Photos_Go_Seven_Days_After_Completion_Unless_An_Open_Dispute_Still_Needs_Them()
    {
        var backlog = RetentionDefaults.BatchSize + 1;
        var blobClient = new Mock<IBlobContainerClient>();
        blobClient
            .Setup(c => c.DeleteAsync(It.Is<string>(name => name.Contains(BlobFailsOrderId)), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("storage unavailable"));

        await TestMethod(
            setup: services =>
            {
                var factory = new Mock<IBlobContainerClientFactory>();
                factory.Setup(f => f.GetBlobContainerClient(Constants.BlobContainers.OrderPhotos)).Returns(blobClient.Object);
                services.Replace(ServiceDescriptor.Singleton(_ => factory.Object));
                services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
                return Task.CompletedTask;
            },
            arrange: async context =>
            {
                SeedCatalogueAndCleaner(context);

                context.Orders.AddRange(
                    CompletedOrder(PastWindowOrderId, completedDaysAgo: 8),
                    CompletedOrder(InsideWindowOrderId, completedDaysAgo: 6),
                    NeverCompletedOrder(NeverCompletedOrderId),
                    CompletedOrder(OpenDisputeOrderId, completedDaysAgo: 30),
                    CompletedOrder(ClosedDisputeOrderId, completedDaysAgo: 30),
                    CompletedOrder(BlobFailsOrderId, completedDaysAgo: 8));

                context.AddRange(Enumerable.Range(0, backlog).Select(i => Photo(PastWindowOrderId, i)));
                context.AddRange(
                    Photo(InsideWindowOrderId, 0),
                    Photo(NeverCompletedOrderId, 0),
                    Photo(OpenDisputeOrderId, 0),
                    Photo(ClosedDisputeOrderId, 0),
                    Photo(BlobFailsOrderId, 0));

                var open = new Dispute(OpenDisputeOrderId, null, DisputeReason.QualityIssue, "Streaks on the windows.", "seed");
                open.UpdateStatus(DisputeStatus.UnderReview, "admin-photo-ret");
                var closed = new Dispute(ClosedDisputeOrderId, null, DisputeReason.QualityIssue, "Streaks on the windows.", "seed");
                closed.UpdateStatus(DisputeStatus.Closed, "admin-photo-ret");
                context.Disputes.AddRange(open, closed);

                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var remaining = await context.Set<OrderPhoto>().IgnoreQueryFilters()
                    .GroupBy(p => p.OrderId)
                    .Select(g => new { OrderId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(g => g.OrderId, g => g.Count);

                Assert.False(remaining.ContainsKey(PastWindowOrderId));
                Assert.False(remaining.ContainsKey(ClosedDisputeOrderId));
                Assert.Equal(1, remaining[InsideWindowOrderId]);
                Assert.Equal(1, remaining[NeverCompletedOrderId]);
                Assert.Equal(1, remaining[OpenDisputeOrderId]);
                Assert.Equal(1, remaining[BlobFailsOrderId]);

                blobClient.Verify(c => c.DeleteAsync($"2026/{PastWindowOrderId}/{backlog - 1}.jpg", It.IsAny<CancellationToken>()), Times.Once);
                blobClient.Verify(c => c.DeleteAsync($"2026/{ClosedDisputeOrderId}/0.jpg", It.IsAny<CancellationToken>()), Times.Once);
                blobClient.Verify(c => c.DeleteAsync(It.Is<string>(name => name.Contains(InsideWindowOrderId)), It.IsAny<CancellationToken>()), Times.Never);
                blobClient.Verify(c => c.DeleteAsync(It.Is<string>(name => name.Contains(OpenDisputeOrderId)), It.IsAny<CancellationToken>()), Times.Never);
                blobClient.Verify(c => c.DeleteAsync(It.Is<string>(name => name.Contains(BlobFailsOrderId)), It.IsAny<CancellationToken>()), Times.Once);
            });
    }

    [Theory]
    [InlineData(DisputeStatus.Pending, true)]
    [InlineData(DisputeStatus.UnderReview, true)]
    [InlineData(DisputeStatus.WaitingForResponse, true)]
    [InlineData(DisputeStatus.Escalated, true)]
    [InlineData(DisputeStatus.Resolved, false)]
    [InlineData(DisputeStatus.Closed, false)]
    public async Task Only_Nonterminal_Disputes_Keep_Photos_Past_Their_Window(DisputeStatus status, bool keepsPhotos)
    {
        var blobClient = new Mock<IBlobContainerClient>();

        await TestMethod(
            setup: services => WithSweep(services, blobClient),
            arrange: async context =>
            {
                SeedCatalogueAndCleaner(context);
                context.Orders.Add(CompletedOrder(OpenDisputeOrderId, completedDaysAgo: 8));
                context.Add(Photo(OpenDisputeOrderId, 0));

                var dispute = new Dispute(OpenDisputeOrderId, null, DisputeReason.QualityIssue, "Streaks on the windows.", "seed");
                if (status == DisputeStatus.Resolved)
                {
                    dispute.Resolve("admin-photo-ret", null, "Resolved without a refund.");
                }
                else if (status != DisputeStatus.Pending)
                {
                    Assert.True(dispute.UpdateStatus(status, "admin-photo-ret"));
                }
                Assert.Equal(status, dispute.Status);
                context.Disputes.Add(dispute);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                Assert.Equal(keepsPhotos, await context.Set<OrderPhoto>().IgnoreQueryFilters().AnyAsync());
                blobClient.Verify(c => c.DeleteAsync($"2026/{OpenDisputeOrderId}/0.jpg", It.IsAny<CancellationToken>()),
                    keepsPhotos ? Times.Never() : Times.Once());
            });
    }

    [Theory]
    [InlineData(14, 3, true, false)]
    [InlineData(3, 14, false, true)]
    public async Task Each_Orders_Company_Controls_Its_Window_Regardless_Of_The_Photos_Company(
        int defaultCompanyDays, int secondCompanyDays, bool keepsDefaultPhoto, bool keepsSecondPhoto)
    {
        var blobClient = new Mock<IBlobContainerClient>();

        await TestMethod(
            setup: services => WithSweep(services, blobClient),
            arrange: async context =>
            {
                SeedCatalogueAndCleaner(context);
                var defaultWindow = TenantConfiguration.Create(RetentionDefaults.OrderPhotosDaysKey, defaultCompanyDays.ToString());
                defaultWindow.TenantId = TestTenants.Default;
                var secondWindow = TenantConfiguration.Create(RetentionDefaults.OrderPhotosDaysKey, secondCompanyDays.ToString());
                secondWindow.TenantId = TestTenants.Second;
                context.TenantConfigurations.AddRange(defaultWindow, secondWindow);

                var defaultOrder = CompletedOrder(PastWindowOrderId, completedDaysAgo: 8);
                defaultOrder.TenantId = TestTenants.Default;
                var secondOrder = CompletedOrder(InsideWindowOrderId, completedDaysAgo: 8);
                secondOrder.TenantId = TestTenants.Second;
                secondOrder.CustomerAddress!.TenantId = TestTenants.Second;
                context.Orders.AddRange(defaultOrder, secondOrder);

                // Both photos carry the uploader's company, including the other operator's order.
                var defaultPhoto = Photo(PastWindowOrderId, 0);
                defaultPhoto.TenantId = TestTenants.Default;
                var secondPhoto = Photo(InsideWindowOrderId, 0);
                secondPhoto.TenantId = TestTenants.Default;
                context.AddRange(defaultPhoto, secondPhoto);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var remainingOrderIds = await context.Set<OrderPhoto>().IgnoreQueryFilters()
                    .Select(p => p.OrderId).ToListAsync();
                Assert.Equal(keepsDefaultPhoto, remainingOrderIds.Contains(PastWindowOrderId));
                Assert.Equal(keepsSecondPhoto, remainingOrderIds.Contains(InsideWindowOrderId));
                blobClient.Verify(c => c.DeleteAsync($"2026/{PastWindowOrderId}/0.jpg", It.IsAny<CancellationToken>()),
                    keepsDefaultPhoto ? Times.Never() : Times.Once());
                blobClient.Verify(c => c.DeleteAsync($"2026/{InsideWindowOrderId}/0.jpg", It.IsAny<CancellationToken>()),
                    keepsSecondPhoto ? Times.Never() : Times.Once());
            });
    }

    private static Task WithSweep(IServiceCollection services, Mock<IBlobContainerClient> blobClient)
    {
        var factory = new Mock<IBlobContainerClientFactory>();
        factory.Setup(f => f.GetBlobContainerClient(Constants.BlobContainers.OrderPhotos)).Returns(blobClient.Object);
        services.Replace(ServiceDescriptor.Singleton(_ => factory.Object));
        services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
        return Task.CompletedTask;
    }

    private static void SeedCatalogueAndCleaner(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var cleanerUser = User.CreateWithPassword("cleaner-photo-ret@cleansia.test", "Seed-Password-123", "Clean", "Er", UserProfile.Employee);
        cleanerUser.Id = CleanerUserId;
        var cleaner = Employee.CreateWithUser(cleanerUser);
        cleaner.Id = CleanerId;
        context.Users.Add(cleanerUser);
        context.Employees.Add(cleaner);
    }

    private static Order CompletedOrder(string id, int completedDaysAgo)
    {
        var order = NewOrder(id, DateTime.UtcNow.AddDays(-completedDaysAgo));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        order.MarkCompletedAt(DateTime.UtcNow.AddDays(-completedDaysAgo));
        return order;
    }

    private static Order NeverCompletedOrder(string id)
    {
        var order = NewOrder(id, DateTime.UtcNow.AddDays(-30));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
        return order;
    }

    private static Order NewOrder(string id, DateTime cleaningDateTime)
    {
        var order = Order.Create(
            customerName: "Photo Customer",
            customerEmail: "photo.customer@cleansia.test",
            customerPhone: "+420777111333",
            customerAddress: Address.Create($"Ulice {id}", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1250m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid);
        order.Id = id;
        return order;
    }

    private static OrderPhoto Photo(string orderId, int index) =>
        OrderPhoto.Create(
            orderId, PhotoType.After,
            $"https://account.blob.core.windows.net/order-photos/2026/{orderId}/{index}.jpg",
            $"{index}.jpg", $"{index}.jpg", 1024, "image/jpeg", CleanerId);
}
