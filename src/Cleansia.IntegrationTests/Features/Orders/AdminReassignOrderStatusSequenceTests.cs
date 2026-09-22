using System.Security.Claims;
using Cleansia.Core.AppServices.Features.Orders;
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

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The reassign writes <c>Confirmed</c> onto a <c>New</c> order, and <c>Order.AddOrderStatus</c> derives
/// the row's <c>Sequence</c> from the history it has in memory. Loaded without that history, the new row
/// took sequence 0 — the creation row's — and the tie-break that makes "current status" deterministic
/// had two rows claiming the same place. Only real Postgres can show it: a mocked queryable hands the
/// handler the whole graph whatever it asked for.
/// </summary>
[Collection("PostgresCollection")]
public class AdminReassignOrderStatusSequenceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string AdminId = "admin-reassign-seq";
    private const string CustomerId = "user-reassign-seq-cust";
    private const string CleanerId = "emp-reassign-seq-1";
    private const string OrderId = "order-reassign-seq";
    private const string CurrencyId = "currency-czk-reassign-seq";
    private const string CountryId = "country-cz-reassign-seq";

    private static Task AdminSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            AdminId, "admin-reassign-seq@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }

    private static async Task Seed(CleansiaDbContext context)
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

        var customer = User.CreateWithPassword("reassign-seq@cleansia.test", "Seed-Password-123", "Jana", "Nováková");
        customer.Id = CustomerId;
        context.Users.Add(customer);

        var cleanerUser = User.CreateWithPassword("emp-reassign-seq@cleansia.test", "Seed-Password-123", "Emp", "Loyee", UserProfile.Employee);
        cleanerUser.Id = $"{CleanerId}-user";
        var cleaner = Employee.CreateWithUser(cleanerUser);
        cleaner.Id = CleanerId;
        cleaner.UpdateContractStatus(ContractStatus.Approved);
        context.Employees.Add(cleaner);

        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "reassign-seq@cleansia.test",
            customerPhone: "+420777111333",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1250m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerId);
        order.Id = OrderId;
        order.SetMaxEmployees(1);
        var creation = OrderStatusTrack.Create(OrderStatus.New, order);
        creation.Created("seed", DateTimeOffset.UtcNow.AddDays(-1));
        order.AddOrderStatus(creation);
        context.Orders.Add(order);

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_Confirmed_Row_An_Assignment_Writes_Takes_The_Next_Sequence_Not_The_Creation_Rows()
    {
        await TestMethod(
            setup: AdminSession,
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new AdminReassignOrder.Command(OrderId, FromEmployeeId: null, ToEmployeeId: CleanerId)),
            assert: async (CleansiaDbContext context, BusinessResult<AdminReassignOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var order = await context.Orders.IgnoreQueryFilters()
                    .Include(o => o.OrderStatusHistory)
                    .Include(o => o.AssignedEmployees)
                    .AsSplitQuery()
                    .SingleAsync(o => o.Id == OrderId);

                Assert.Equal(CleanerId, Assert.Single(order.AssignedEmployees).EmployeeId);
                Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
                var rows = order.OrderStatusHistory.OrderBy(s => s.Sequence).ToList();
                Assert.Equal([OrderStatus.New, OrderStatus.Confirmed], rows.Select(s => s.Status));
                Assert.Equal([0, 1], rows.Select(s => s.Sequence));
            },
            transactional: false);
    }
}
