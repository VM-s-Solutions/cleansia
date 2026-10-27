using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The 15-minute tick's fourth command on real Postgres, through the host's own pipeline: its selection
/// translates, each job it selects is completed in a scope of its own and committed there, and every row
/// that completion writes carries the job's company, since a sweep has no JWT. Owner ruling 2026-10-26: a
/// job closes itself only when every cleaner on it marked done and nobody pressed Complete.
/// </summary>
[Collection("PostgresCollection")]
public class AutoCompleteMarkedDoneOrdersPostgresTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-auto-close";
    private const string CurrencyId = "currency-czk-auto-close";
    private const string Closes = "order-close-closes";
    private const string StillCleaning = "order-close-cleaning";
    private const string MarkedLately = "order-close-lately";
    private const string CleanerA = "emp-auto-close-pg-a";
    private const string CleanerB = "emp-auto-close-pg-b";

    [Fact]
    public async Task The_Tick_Completes_Only_The_Job_Whose_Whole_Crew_Marked_Done_Two_Hours_Ago_Under_Its_Own_Company()
    {
        await TestMethod(
            setup: NoAmbientCompany,
            arrange: async (CleansiaDbContext context) =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
                country.Id = CountryId;
                context.Countries.Add(country);
                var currency = Currency.Create("CZK", "Kč", "Czech koruna");
                currency.Id = CurrencyId;
                currency.IsActive = true;
                context.Currencies.Add(currency);

                var crew = new[] { Cleaner(CleanerA), Cleaner(CleanerB) };
                context.Employees.AddRange(crew);

                var now = DateTime.UtcNow;
                var startedAt = now.AddHours(-4);
                context.Orders.AddRange(
                    Job(Closes, crew, startedAt, now.AddHours(-3), now.AddMinutes(-150)),
                    Job(StillCleaning, crew, startedAt, now.AddHours(-3), null),
                    Job(MarkedLately, crew, startedAt, now.AddHours(-3), now.AddMinutes(-30)));
                foreach (var orderId in new[] { Closes, StillCleaning, MarkedLately })
                {
                    context.Add(OrderPhoto.Create(
                        orderId, PhotoType.After, $"https://blob.test/{orderId}.jpg", $"{orderId}.jpg", "after.jpg",
                        1024, "image/jpeg", CleanerA));
                }

                // This scope has no company, as the sweep's has none: the seed names the fixture's.
                StampUnstampedAdded(context, TestTenants.Default);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
                await provider.GetRequiredService<IMediator>().Send(new AutoCompleteMarkedDoneOrders.Command()),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Equal(new AutoCompleteMarkedDoneOrders.Response(Completed: 1, Considered: 1), result.Value);

                var orders = await context.Orders.IgnoreQueryFilters()
                    .Include(o => o.OrderStatusHistory)
                    .AsSplitQuery()
                    .ToDictionaryAsync(o => o.Id);
                var closed = orders[Closes];
                Assert.Equal(OrderStatus.Completed, closed.CurrentStatus);
                Assert.Equal(90, closed.ActualCompletionTime);
                Assert.Equal(
                    TestTenants.Default,
                    Assert.Single(closed.OrderStatusHistory, s => s.Status == OrderStatus.Completed).TenantId);
                Assert.Equal(OrderStatus.InProgress, orders[StillCleaning].CurrentStatus);
                Assert.Equal(OrderStatus.InProgress, orders[MarkedLately].CurrentStatus);

                var payKeys = await context.OutboxMessages.IgnoreQueryFilters()
                    .Where(m => m.QueueName == QueueNames.CalculateOrderPay)
                    .Select(m => m.MessageKey)
                    .ToListAsync();
                Assert.Equal(
                    new[] { MessageKeys.Pay(Closes, CleanerA), MessageKeys.Pay(Closes, CleanerB) }.Order(),
                    payKeys.Order());
            },
            transactional: false);
    }

    /// <summary>A system job's scope: no claim and no override, so no company until the unit takes the job's.</summary>
    private static Task NoAmbientCompany(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<ITenantProvider>(sp =>
            new TenantProvider(sp.GetRequiredService<IHttpContextAccessor>())));
        return Task.CompletedTask;
    }

    private static Employee Cleaner(string employeeId)
    {
        var user = User.CreateWithPassword(
            $"{employeeId}@cleansia.test", "Seed-Password-123", "Emp", "Loyee", UserProfile.Employee);
        user.Id = $"{employeeId}-user";
        var cleaner = Employee.CreateWithUser(user);
        cleaner.Id = employeeId;
        cleaner.UpdateContractStatus(ContractStatus.Approved);
        cleaner.AssignWorkCountry(CountryId);
        return cleaner;
    }

    /// <summary>A two-seat card job, paid, booked from no account, under way since <paramref name="startedAt"/>.</summary>
    private static Order Job(string orderId, Employee[] crew, DateTime startedAt, DateTime firstMark, DateTime? lastMark)
    {
        var order = Order.Create(
            customerName: "Guest Customer",
            customerEmail: $"{orderId}@cleansia.test",
            customerPhone: "+420777111777",
            customerAddress: Address.Create("Testovaci 16", "Praha", "11000", CountryId),
            rooms: 3,
            bathrooms: 2,
            cleaningDateTime: startedAt,
            paymentType: PaymentType.Card,
            totalPrice: 2400m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = orderId;
        order.SetMaxEmployees(2);
        var stamp = new DateTimeOffset(startedAt.AddHours(-2), TimeSpan.Zero);
        foreach (var status in new[] { OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress })
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("seed", status == OrderStatus.InProgress ? new DateTimeOffset(startedAt, TimeSpan.Zero) : stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(30);
        }

        foreach (var (cleaner, mark) in new[] { (crew[0], (DateTime?)firstMark), (crew[1], lastMark) })
        {
            var seat = OrderEmployee.Create(order, cleaner);
            if (mark is not null)
            {
                seat.MarkCompletionConfirmed(mark.Value);
            }

            order.AddAssignedEmployee(seat);
        }

        return order;
    }
}
