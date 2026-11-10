using System.Globalization;
using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The reminder sweep's two no-cleaner steps on real Postgres, through the mediator as the timer sends it.
/// <c>MockQueryable</c> runs any shape, so only Postgres shows that the composed money term, the hold
/// term, the quiet period's history <c>Any</c> and the confirmation term translate, and pick the job due a
/// day ahead and the job due within two hours out of a board of unstaffed ones. A second tick writes
/// nothing: each key's guard finds the feed row the first tick committed, where a repeat would otherwise
/// collide on the e-mail's outbox key.
/// </summary>
[Collection("PostgresCollection")]
public class NoCleanerYetAlertPostgresTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string SupportId = "nc-admin-support";
    private const string SupportEmail = SupportId + "@cleansia.test";
    private const string DueOrderId = "nc-order-due";
    private const string SoonOrderId = "nc-order-soon";

    [Fact]
    public async Task Only_The_Due_Jobs_Are_Reported_Under_Their_Own_Keys_And_A_Second_Tick_Writes_Nothing()
    {
        var now = DateTime.UtcNow;

        await TestMethod(
            arrange: context => SeedAsync(context, now),
            act: async provider => (First: await TickAsync(provider), Second: await TickAsync(provider)),
            assert: async (CleansiaDbContext context, (SendCleanerJobReminders.Response First, SendCleanerJobReminders.Response Second) ticks) =>
            {
                Assert.Equal(1, ticks.First.NoCleanerAlerts);
                Assert.Equal(1, ticks.First.NoCleanerSoonAlerts);
                Assert.Equal(0, ticks.Second.NoCleanerAlerts);
                Assert.Equal(0, ticks.Second.NoCleanerSoonAlerts);

                var rows = await context.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
                Assert.Equal(2, rows.Count);
                var urgent = Assert.Single(rows, r => r.EventKey == AdminNotificationEventCatalog.OrderNoCleanerSoon);
                Assert.Equal(SoonOrderId, JsonSerializer.Deserialize<Dictionary<string, string>>(urgent.ArgsJson)!["orderId"]);
                var feed = Assert.Single(rows, r => r.EventKey == AdminNotificationEventCatalog.OrderNoCleanerYet);
                Assert.Equal(AdminNotificationEventCatalog.OrderNoCleanerYet, feed.EventKey);
                Assert.Equal(SupportId, feed.UserId);
                Assert.Equal(TestTenants.Default, feed.TenantId);
                var args = JsonSerializer.Deserialize<Dictionary<string, string>>(feed.ArgsJson)!;
                Assert.Equal(DueOrderId, args["orderId"]);
                var due = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == DueOrderId);
                Assert.Equal(due.DisplayOrderNumber, args["orderNumber"]);
                Assert.Equal(
                    DateTime.SpecifyKind(due.CleaningDateTime, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
                    args["cleaningDateTime"]);

                var emails = await context.OutboxMessages.IgnoreQueryFilters().ToListAsync();
                Assert.All(emails, email => Assert.Equal(QueueNames.SendEmail, email.QueueName));
                Assert.All(emails, email => Assert.Equal(TestTenants.Default, email.TenantId));
                Assert.Equal(
                    new[]
                    {
                        MessageKeys.AdminNotificationEmail(AdminNotificationEventCatalog.OrderNoCleanerSoon, SoonOrderId, SupportEmail),
                        MessageKeys.AdminNotificationEmail(AdminNotificationEventCatalog.OrderNoCleanerYet, DueOrderId, SupportEmail),
                    }.Order(),
                    emails.Select(email => email.MessageKey).Order());
            },
            transactional: false);
    }

    private static async Task<SendCleanerJobReminders.Response> TickAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new SendCleanerJobReminders.Command());
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    /// <summary>
    /// One due job and, beside it, one job per term that each fails that term alone: already past its start
    /// (its three-hour slot still open, so still offerable), too far out, still reserved, a status change
    /// inside the hour, a customer confirmation inside the hour, an unpaid card checkout, and a cleaner on it.
    /// </summary>
    private static async Task SeedAsync(CleansiaDbContext context, DateTime now)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = "nc-country";
        var currency = Currency.Create("CZK", "Kc", "Czech koruna");
        currency.Id = "nc-currency";
        context.Countries.Add(country);
        context.Currencies.Add(currency);

        var support = User.CreateWithPassword(
            SupportEmail, "Seed-Password-123", "Sup", "Port", UserProfile.Administrator, adminRole: AdminRole.Support);
        support.Id = SupportId;
        support.TenantId = TestTenants.Default;
        support.ConfirmEmail();
        context.Users.Add(support);

        var cleanerUser = User.CreateWithPassword("cleaner-no-cleaner@cleansia.test", "Seed-Password-123", "Clea", "Ner");
        cleanerUser.Id = "nc-user-cleaner";
        var cleaner = Employee.CreateWithUser(cleanerUser);
        cleaner.Id = "nc-employee";
        cleaner.UpdateContractStatus(ContractStatus.Approved);
        context.Users.Add(cleanerUser);
        context.Employees.Add(cleaner);

        Order Job(string id, double hoursOut = 20, PaymentType paymentType = PaymentType.Cash,
            PaymentStatus paymentStatus = PaymentStatus.Pending, string? recurringTemplateId = null,
            OrderStatus status = OrderStatus.New, double statusMinutesAgo = 180)
        {
            var order = Order.Create(
                customerName: "No Cleaner Customer",
                customerEmail: $"{id}@cleansia.test",
                customerPhone: "+420000000000",
                customerAddress: Address.Create("Test St 1", "Brno", "60200", country.Id),
                rooms: 1,
                bathrooms: 1,
                cleaningDateTime: now.AddHours(hoursOut),
                paymentType: paymentType,
                totalPrice: 1000m,
                currencyId: currency.Id,
                paymentStatus: paymentStatus,
                recurringTemplateId: recurringTemplateId,
                cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            order.Id = id;
            order.SetMaxEmployees(1);
            var row = OrderStatusTrack.Create(status, order);
            row.Created("seed", now.AddMinutes(-statusMinutesAgo));
            order.AddOrderStatus(row);
            context.Orders.Add(order);
            return order;
        }

        Job(DueOrderId);
        Job(SoonOrderId, hoursOut: 1.5);
        Job("nc-order-soon-fresh", hoursOut: 1.5, statusMinutesAgo: 30);
        Job("nc-order-started", hoursOut: -0.5).UpdateEstimatedTime(180);
        Job("nc-order-far", hoursOut: 30);
        Job("nc-order-reserved").GrantPreferredHold("emp-chosen", untilUtc: now.AddHours(1), nowUtc: now, maxRounds: 2);
        Job("nc-order-fresh", statusMinutesAgo: 30);
        Job("nc-order-just-confirmed", recurringTemplateId: "nc-template").ConfirmByCustomer(now.AddMinutes(-30));
        Job("nc-order-unpaid-card", paymentType: PaymentType.Card);
        var staffed = Job("nc-order-staffed", status: OrderStatus.Confirmed);
        staffed.AddAssignedEmployee(OrderEmployee.Create(staffed, cleaner));

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }
}
