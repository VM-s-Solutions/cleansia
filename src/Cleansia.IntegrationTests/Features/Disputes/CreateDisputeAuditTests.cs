using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Disputes;

/// <summary>
/// ADR-0062 D3 through the real pipeline on real Postgres: a refused filing leaves ONE out-of-band
/// failure row keyed on the ORDER it was refused against (the marker's resource), with the refusal KEY
/// and no payload; an accepted filing leaves ONE success row re-labelled to the DISPUTE it created,
/// committed with it, whose payload carries the description's length and never its text.
/// </summary>
[Collection("PostgresCollection")]
public class CreateDisputeAuditTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CustomerId = "user-dispute-audit-1";
    private const string OrderId = "order-dispute-audit-1";
    private const string CurrencyId = "currency-czk-dispute-audit";
    private const string CountryId = "country-cz-dispute-audit";
    private const string Description = "The kitchen floor was not mopped and the bins were left full.";

    private static Task CustomerSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CustomerId, "dispute-audit@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));
        return Task.CompletedTask;
    }

    private static Func<CleansiaDbContext, Task> Seed(DateTime cleaningDateTime) => async context =>
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

        var customer = User.CreateWithPassword("dispute-audit@cleansia.test", "Seed-Password-123", "Dispute", "Audit");
        customer.Id = CustomerId;
        context.Users.Add(customer);

        var order = Order.Create(
            customerName: "Dispute Audit",
            customerEmail: "dispute-audit@cleansia.test",
            customerPhone: "+420777111333",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime,
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
    };

    private static async Task<List<CustomerActionAudit>> CustomerRows(CleansiaDbContext context) =>
        await context.CustomerActionAudits.IgnoreQueryFilters().ToListAsync();

    [Fact]
    public async Task A_Filing_Before_The_Clean_Leaves_One_Failure_Row_On_The_Order_With_The_Key_And_No_Dispute()
    {
        await TestMethod(
            setup: CustomerSession,
            arrange: Seed(cleaningDateTime: DateTime.UtcNow.AddDays(1)),
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new CreateDispute.Command(OrderId, DisputeReason.QualityIssue, Description)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateDispute.Response> result) =>
            {
                Assert.True(result.IsFailure);
                Assert.Equal(BusinessErrorMessage.DisputeCleaningNotStarted, result.Error!.Message);
                Assert.Empty(await context.Disputes.IgnoreQueryFilters().ToListAsync());

                var row = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.dispute.create", row.Action);
                Assert.False(row.Success);
                Assert.Equal(BusinessErrorMessage.DisputeCleaningNotStarted, row.ErrorCode);
                Assert.Equal("Order", row.ResourceType);
                Assert.Equal(OrderId, row.ResourceId);
                Assert.Equal(CustomerId, row.UserId);
                Assert.Null(row.PayloadJson);
                Assert.Equal(TestTenants.Default, row.TenantId);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Filing_After_The_Clean_Leaves_One_Success_Row_On_The_Dispute_With_The_Length_And_Never_The_Text()
    {
        await TestMethod(
            setup: CustomerSession,
            arrange: Seed(cleaningDateTime: DateTime.UtcNow.AddHours(-6)),
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new CreateDispute.Command(OrderId, DisputeReason.QualityIssue, Description)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateDispute.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var dispute = await context.Disputes.IgnoreQueryFilters().SingleAsync(d => d.Id == result.Value.DisputeId);
                Assert.Equal(Description, dispute.Description);

                var row = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.dispute.create", row.Action);
                Assert.True(row.Success);
                Assert.Equal("Dispute", row.ResourceType);
                Assert.Equal(dispute.Id, row.ResourceId);
                Assert.Equal(CustomerId, row.UserId);

                var payload = JsonDocument.Parse(row.PayloadJson!).RootElement;
                Assert.Equal(dispute.Id, payload.GetProperty("disputeId").GetString());
                Assert.Equal(OrderId, payload.GetProperty("orderId").GetString());
                Assert.Equal("qualityIssue", payload.GetProperty("reason").GetString());
                Assert.InRange(payload.GetProperty("hoursSinceCompletion").GetDecimal(), 5.9m, 6.2m);
                Assert.Equal(DisputeLimits.FilingWindowHours, payload.GetProperty("filingWindowHours").GetInt32());
                Assert.Equal(Description.Length, payload.GetProperty("descriptionLength").GetInt32());
                Assert.Equal(1250m, payload.GetProperty("orderTotalPrice").GetDecimal());
                Assert.Equal("CZK", payload.GetProperty("currencyCode").GetString());
                Assert.DoesNotContain(payload.EnumerateObject(), p => p.Name == "description");
                Assert.DoesNotContain("kitchen", row.PayloadJson);
            },
            transactional: false);
    }
}
