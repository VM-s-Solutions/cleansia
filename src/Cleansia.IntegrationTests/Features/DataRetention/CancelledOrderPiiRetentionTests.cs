using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.DataRetention;

/// <summary>
/// A cancelled booking holds the same name, contact, address and door details a completed one does, with
/// no service record to defend, so it is anonymised on the same clock: two years from the cleaning date
/// (owner ruling 2026-09-28). The anonymised order also loses its floor, flat, access mode and the
/// customer's own cancellation words; a platform reason code stays. Through the real sweep on Postgres.
/// </summary>
[Collection("PostgresCollection")]
public class CancelledOrderPiiRetentionTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-cancelled-pii";
    private const string CurrencyId = "currency-czk-cancelled-pii";
    private const string CustomerName = "Milada Novotna";

    private const string OldCancelledOrderId = "order-cpii-old-cancelled";
    private const string RecentCancelledOrderId = "order-cpii-recent-cxl";
    private const string OldCompletedOrderId = "order-cpii-old-completed";
    private const string OldWindDownOrderId = "order-cpii-old-wind-down";
    private const string OldInProgressOrderId = "order-cpii-old-in-progress";
    private const string RetractedOccurrenceId = "order-cpii-retracted";

    [Fact]
    public async Task A_Cancelled_Order_Is_Anonymised_Two_Years_After_Its_Cleaning_Date_Like_A_Completed_One()
    {
        await TestMethod(
            setup: RetentionServices,
            arrange: async context =>
            {
                SeedCatalogue(context);
                context.Orders.AddRange(
                    Cancelled(OldCancelledOrderId, yearsAgo: 3, CancelledBy.Customer, "My ex lives there now"),
                    Cancelled(RecentCancelledOrderId, yearsAgo: 1, CancelledBy.Customer, "Changed my plans"),
                    Completed(OldCompletedOrderId, yearsAgo: 3),
                    Cancelled(OldWindDownOrderId, yearsAgo: 3, CancelledBy.System, OrderCancellationReasons.CompanyWindDown),
                    InProgress(OldInProgressOrderId, yearsAgo: 3));
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var orders = await context.Orders.IgnoreQueryFilters()
                    .Include(o => o.CustomerAddress)
                    .ToDictionaryAsync(o => o.Id);

                var oldCancelled = orders[OldCancelledOrderId];
                AssertAnonymised(oldCancelled);
                Assert.Null(oldCancelled.CancellationReason);

                AssertAnonymised(orders[OldCompletedOrderId]);

                var windDown = orders[OldWindDownOrderId];
                AssertAnonymised(windDown);
                Assert.Equal(OrderCancellationReasons.CompanyWindDown, windDown.CancellationReason);

                var recent = orders[RecentCancelledOrderId];
                Assert.Equal(CustomerName, recent.CustomerName);
                Assert.Equal("3", recent.CustomerFloor);
                Assert.Equal("Changed my plans", recent.CancellationReason);

                Assert.Equal(CustomerName, orders[OldInProgressOrderId].CustomerName);
            });
    }

    /// <summary>
    /// Anonymising an occurrence the recurring sweep retracted clears its template and its owner, so it reads
    /// like a one-off card checkout nobody paid for. The abandoned-checkout sweep must still see a cancelled
    /// booking: the reason, the date and the history of its cancellation stay as they were, and no e-mail is
    /// staged for an address that is now the anonymisation marker.
    /// </summary>
    [Fact]
    public async Task An_Anonymised_Retracted_Recurring_Occurrence_Is_Not_Swept_Again_As_An_Abandoned_Checkout()
    {
        var cancelledAt = DateTime.UtcNow.Date.AddYears(-3);
        await TestMethod(
            setup: RetentionServices,
            arrange: async context =>
            {
                SeedCatalogue(context);
                var owner = User.CreateWithPassword("recurring@cleansia.test", "Password1!", "Milada", "Novotna", UserProfile.Customer);
                owner.Id = "user-cpii-recurring";
                context.Users.Add(owner);
                var occurrence = Order.Create(
                    customerName: CustomerName,
                    customerEmail: "recurring@cleansia.test",
                    customerPhone: "+420777111222",
                    customerAddress: Address.Create("Ulice recurring", "Praha", "11000", CountryId),
                    rooms: 2,
                    bathrooms: 1,
                    cleaningDateTime: cancelledAt.AddHours(10),
                    paymentType: PaymentType.Card,
                    totalPrice: 1250m,
                    currencyId: CurrencyId,
                    paymentStatus: PaymentStatus.Pending,
                    userId: owner.Id,
                    recurringTemplateId: "tmpl-cpii-recurring",
                    cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
                occurrence.Id = RetractedOccurrenceId;
                occurrence.Created("seed", new DateTimeOffset(cancelledAt.AddDays(-7)));
                occurrence.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, occurrence));
                occurrence.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, occurrence));
                occurrence.Cancel(cancelledAt, CancelledBy.System, 0m, 0m, OrderCancellationReasons.RecurringNotConfirmed);
                context.Orders.Add(occurrence);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
                using var sweep = provider.CreateScope();
                var result = await ActivatorUtilities.CreateInstance<CleanupStalePendingOrders.Handler>(sweep.ServiceProvider)
                    .Handle(new CleanupStalePendingOrders.Command(), CancellationToken.None);
                return result.Value!.CancelledCount;
            },
            assert: async (CleansiaDbContext context, int cancelledCount) =>
            {
                Assert.Equal(0, cancelledCount);
                var order = await context.Orders.IgnoreQueryFilters()
                    .Include(o => o.OrderStatusHistory)
                    .SingleAsync(o => o.Id == RetractedOccurrenceId);
                Assert.Equal(AnonymizationMarker.Value, order.CustomerEmail);
                Assert.Null(order.RecurringTemplateId);
                Assert.Equal(CancelledBy.System, order.CancelledBy);
                Assert.Equal(OrderCancellationReasons.RecurringNotConfirmed, order.CancellationReason);
                Assert.Equal(cancelledAt, order.CancelledAt);
                Assert.Single(order.OrderStatusHistory, track => track.Status == OrderStatus.Cancelled);
                Assert.False(await context.OutboxMessages.IgnoreQueryFilters()
                    .AnyAsync(m => m.QueueName == QueueNames.SendEmail));
            },
            transactional: false);
    }

    private static Task RetentionServices(IServiceCollection services)
    {
        var factory = new Mock<IBlobContainerClientFactory>();
        factory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(Mock.Of<IBlobContainerClient>());
        services.Replace(ServiceDescriptor.Singleton(_ => factory.Object));
        services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
        return Task.CompletedTask;
    }

    private static void AssertAnonymised(Order order)
    {
        Assert.Equal(AnonymizationMarker.Value, order.CustomerName);
        Assert.Equal(AnonymizationMarker.Value, order.CustomerEmail);
        Assert.Equal(AnonymizationMarker.Value, order.CustomerPhone);
        Assert.Null(order.AccessInstructions);
        Assert.Null(order.CustomerFloor);
        Assert.Null(order.CustomerApartment);
        Assert.Null(order.AccessMode);
        Assert.Equal(AnonymizationMarker.Value, order.CustomerAddress!.Street);
    }

    private static void SeedCatalogue(CleansiaDbContext context)
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
    }

    private static Order Cancelled(string id, int yearsAgo, CancelledBy cancelledBy, string reason)
    {
        var order = NewOrder(id, yearsAgo);
        order.Cancel(DateTime.UtcNow.AddYears(-yearsAgo).AddDays(-1), cancelledBy, 0m, 0m, reason);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
        return order;
    }

    private static Order Completed(string id, int yearsAgo)
    {
        var order = NewOrder(id, yearsAgo);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        return order;
    }

    private static Order InProgress(string id, int yearsAgo)
    {
        var order = NewOrder(id, yearsAgo);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.InProgress, order));
        return order;
    }

    private static Order NewOrder(string id, int yearsAgo)
    {
        var order = Order.Create(
            customerName: CustomerName,
            customerEmail: $"{id}@cleansia.test",
            customerPhone: "+420777111222",
            customerAddress: Address.Create($"Ulice {id}", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddYears(-yearsAgo),
            paymentType: PaymentType.Cash,
            totalPrice: 1250m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            accessInstructions: "Code 4455",
            customerFloor: "3",
            customerApartment: "12B",
            accessMode: "door_code",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = id;
        return order;
    }
}
