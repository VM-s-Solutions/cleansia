using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The booking tells the OPERATOR's administrators only when it is offerable as created. A cash
/// one-off at a Slovak address lands in the second company and tells that company's administrator
/// — one <c>admin.order.new</c> row and one outbox e-mail, with the money in the market's currency —
/// and not the first company's. A card booking is an unpaid checkout until the webhook says otherwise,
/// so its creation tells nobody at all.
/// </summary>
public partial class CreateOrderCallerCurrencyTests
{
    private const string AdminOfSecondId = "admin-caller-currency-sk";
    private const string AdminOfDefaultId = "admin-caller-currency-cz";

    private static async Task SeedSlovakiaUnderSecondCompanyWithAdministratorsAsync(CleansiaDbContext context)
    {
        await SeedWithSlovakiaOperatedBySecondCompanyAsync(context);
        foreach (var (id, tenantId) in new[] { (AdminOfSecondId, TestTenants.Second), (AdminOfDefaultId, TestTenants.Default) })
        {
            var admin = User.CreateWithPassword($"{id}@cleansia.test", "Seed-Password-123", "Ad", "Min", UserProfile.Administrator, adminRole: AdminRole.Administrator);
            admin.Id = id;
            admin.TenantId = tenantId;
            admin.ConfirmEmail();
            context.Users.Add(admin);
        }

        await context.CommitAsync(CancellationToken.None);
    }

    private static Task<List<UserNotification>> NewOrderRowsAsync(CleansiaDbContext context) =>
        context.Set<UserNotification>().IgnoreQueryFilters()
            .Where(n => n.EventKey == AdminNotificationEventCatalog.OrderNew)
            .ToListAsync();

    [Fact]
    public async Task A_Cash_Booking_Tells_The_Operating_Companys_Administrators_At_Creation_And_Not_The_Other_Companys()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedSlovakiaUnderSecondCompanyWithAdministratorsAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Slovakia, currencyId: null, EurServicePrice + EurPackagePrice) with { PaymentType = PaymentType.Cash }),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == result.Value.Id);
                Assert.Equal(TestTenants.Second, order.TenantId);

                var row = Assert.Single(await NewOrderRowsAsync(context));
                Assert.Equal(AdminOfSecondId, row.UserId);
                Assert.Equal(TestTenants.Second, row.TenantId);
                var args = JsonSerializer.Deserialize<Dictionary<string, string>>(row.ArgsJson)!;
                Assert.Equal(order.Id, args["orderId"]);
                Assert.Equal(order.DisplayOrderNumber, args["orderNumber"]);
                Assert.Equal("60 €", args["amount"]);
                Assert.Equal(nameof(PaymentType.Cash), args["paymentType"]);
                Assert.Equal(Slovakia, args["countryId"]);
                Assert.DoesNotContain(CustomerEmail, row.ArgsJson);

                var email = Assert.Single(await context.OutboxMessages.IgnoreQueryFilters()
                    .Where(m => m.QueueName == QueueNames.SendEmail && m.Body.Contains(AdminNotificationEventCatalog.OrderNew))
                    .ToListAsync());
                Assert.Equal(TestTenants.Second, email.TenantId);
                Assert.Contains($"{AdminOfSecondId}@cleansia.test", email.Body);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Card_Booking_Tells_Nobody_At_Creation()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedSlovakiaUnderSecondCompanyWithAdministratorsAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Slovakia, currencyId: null, EurServicePrice + EurPackagePrice)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Empty(await NewOrderRowsAsync(context));
                Assert.Empty(await context.OutboxMessages.IgnoreQueryFilters()
                    .Where(m => m.QueueName == QueueNames.SendEmail && m.Body.Contains(AdminNotificationEventCatalog.OrderNew))
                    .ToListAsync());
            },
            transactional: false);
    }
}
