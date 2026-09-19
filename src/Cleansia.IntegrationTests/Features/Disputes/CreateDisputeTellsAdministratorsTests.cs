using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Disputes;

/// <summary>
/// A customer's filing through the real pipeline on real Postgres writes one <c>admin.dispute.filed</c>
/// row per active confirmed administrator of the ORDER's company, committed with the dispute, carrying
/// the ids the console deep-links from and nothing of the customer — and a second company's
/// administrator sees nothing. The same commit lands one send-email outbox row per administrator,
/// each under its own key and addressed in that administrator's language.
/// </summary>
[Collection("PostgresCollection")]
public class CreateDisputeTellsAdministratorsTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CustomerId = "user-dispute-tells-1";
    private const string OrderId = "order-dispute-tells-1";
    private const string CurrencyId = "currency-czk-dispute-tells";
    private const string CountryId = "country-cz-dispute-tells";
    private const string AdminA1 = "admin-dispute-tells-a1";
    private const string AdminA2 = "admin-dispute-tells-a2";
    private const string AdminB1 = "admin-dispute-tells-b1";
    private const string Description = "The kitchen floor was not mopped and the bins were left full.";

    private static Task CustomerSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CustomerId, "dispute-tells@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));
        return Task.CompletedTask;
    }

    private static User Administrator(string id, string tenantId, string? language = null)
    {
        var user = User.CreateWithPassword($"{id}@cleansia.test", "Seed-Password-123", "Ad", "Min", UserProfile.Administrator, language);
        user.Id = id;
        user.TenantId = tenantId;
        user.ConfirmEmail();
        return user;
    }

    private static async Task Seed(CleansiaDbContext context)
    {
        context.Languages.AddRange(Language.Create("en", "English"), Language.Create("uk", "Ukrainian"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var customer = User.CreateWithPassword("dispute-tells@cleansia.test", "Seed-Password-123", "Jana", "Nováková");
        customer.Id = CustomerId;
        context.Users.Add(customer);
        context.Users.AddRange(
            Administrator(AdminA1, TestTenants.Default, language: "uk"),
            Administrator(AdminA2, TestTenants.Default),
            Administrator(AdminB1, TestTenants.Second));

        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "dispute-tells@cleansia.test",
            customerPhone: "+420777111333",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(-6),
            paymentType: PaymentType.Card,
            totalPrice: 1250m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerId);
        order.Id = OrderId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        context.Orders.Add(order);
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_Filing_Writes_One_Row_Per_Administrator_Of_The_Orders_Company_And_None_For_Another()
    {
        await TestMethod(
            setup: CustomerSession,
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new CreateDispute.Command(OrderId, DisputeReason.QualityIssue, Description)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateDispute.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);

                var rows = await context.Set<UserNotification>().IgnoreQueryFilters()
                    .Where(n => n.EventKey == AdminNotificationEventCatalog.DisputeFiled)
                    .OrderBy(n => n.UserId)
                    .ToListAsync();
                Assert.Equal([AdminA1, AdminA2], rows.Select(r => r.UserId));
                Assert.All(rows, row =>
                {
                    Assert.Equal(TestTenants.Default, row.TenantId);
                    Assert.Null(row.ReadOn);
                    var args = JsonSerializer.Deserialize<Dictionary<string, string>>(row.ArgsJson)!;
                    Assert.Equal(result.Value.DisputeId, args["disputeId"]);
                    Assert.Equal(OrderId, args["orderId"]);
                    Assert.Equal(order.DisplayOrderNumber, args["orderNumber"]);
                    Assert.Equal(nameof(DisputeReason.QualityIssue), args["reason"]);
                    Assert.Equal(4, args.Count);
                    Assert.DoesNotContain("Jana", row.ArgsJson);
                    Assert.DoesNotContain("kitchen", row.ArgsJson);
                    Assert.DoesNotContain("@", row.ArgsJson);
                });
                Assert.DoesNotContain(rows, r => r.UserId == AdminB1);

                var emails = await context.OutboxMessages.IgnoreQueryFilters()
                    .Where(m => m.QueueName == QueueNames.SendEmail)
                    .ToListAsync();
                Assert.Equal(2, emails.Count);
                Assert.Equal(2, emails.Select(e => e.MessageKey).Distinct(StringComparer.Ordinal).Count());
                var envelopes = emails
                    .Select(e => JsonSerializer.Deserialize<QueueEnvelope<SendAdminNotificationEmailMessage>>(
                        e.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!)
                    .OrderBy(e => e.Payload.Email)
                    .ToList();
                Assert.Equal(
                    [($"{AdminA1}@cleansia.test", "uk"), ($"{AdminA2}@cleansia.test", "en")],
                    envelopes.Select(e => (e.Payload.Email, e.Payload.LanguageCode)));
                Assert.All(envelopes, e =>
                {
                    Assert.Equal(TestTenants.Default, e.TenantId);
                    Assert.Equal(AdminNotificationEventCatalog.DisputeFiled, e.Payload.EventKey);
                    Assert.Equal(result.Value.DisputeId, e.Payload.Subject);
                    Assert.Equal(order.DisplayOrderNumber, e.Payload.Args["orderNumber"]);
                });
                Assert.All(emails, e => Assert.Equal(TestTenants.Default, e.TenantId));
            },
            transactional: false);
    }
}
