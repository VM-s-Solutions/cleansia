using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Dashboard;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-28, decision 25: a company may cap the cash a cleaner holds; above the cap, cash jobs
/// are hidden from that cleaner's board while card jobs and a cash job they are already on stay. No cap is
/// set by default, and none hides anything.
/// </summary>
public class CashFloatCapBoardTests
{
    private const string Cleaner = "emp-cleaner";

    [Fact]
    public void Above_The_Cap_The_Available_Board_Drops_Cash_Jobs_And_Keeps_Card_Jobs()
    {
        var cashJob = NewOrder("cash", PaymentType.Cash);
        var cardJob = NewOrder("card", PaymentType.Card);

        var board = Filter(
            DashboardSpecifications.CreateAvailableOrdersSpec(Cleaner, "czk", DateTime.UtcNow, cashJobsHidden: true),
            cashJob, cardJob);

        Assert.Equal([cardJob], board);
    }

    [Fact]
    public void At_Or_Below_The_Cap_The_Available_Board_Keeps_Cash_Jobs()
    {
        var cashJob = NewOrder("cash", PaymentType.Cash);

        var board = Filter(DashboardSpecifications.CreateAvailableOrdersSpec(Cleaner, "czk", DateTime.UtcNow), cashJob);

        Assert.Equal([cashJob], board);
    }

    [Fact]
    public void A_Cash_Job_The_Cleaner_Is_Already_On_Stays_On_Their_Lists_Above_The_Cap()
    {
        var cashJob = NewOrder("cash", PaymentType.Cash);
        var cashJobTheyAreOn = NewOrder("theirs", PaymentType.Cash, assignedEmployeeId: Cleaner);

        var listed = Filter(
            Core.Domain.Specifications.OrderSpecification.Create(hideCashFromEmployeeId: Cleaner),
            cashJob, cashJobTheyAreOn);

        Assert.Equal([cashJobTheyAreOn], listed);
    }

    [Fact]
    public async Task Above_The_Cap_A_Cash_Job_Held_For_The_Cleaner_Is_Not_A_Pending_Offer_And_A_Card_Job_Is()
    {
        var heldCashJob = HeldFor(NewOrder("held-cash", PaymentType.Cash));
        var heldCardJob = HeldFor(NewOrder("held-card", PaymentType.Card));
        var orders = new Mock<IOrderRepository>();
        orders.Setup(r => r.GetQueryable()).Returns(new[] { heldCashJob, heldCardJob }.AsQueryable().BuildMock());
        var access = new Mock<IOrderAccessService>();
        access.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Cleaner);
        access
            .Setup(s => s.CashJobsHiddenFromAsync(Cleaner, ValidatorTestHelpers.CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var offers = await new GetMyPendingOffers.Handler(orders.Object, access.Object, ValidatorTestHelpers.CurrencyResolver())
            .Handle(new GetMyPendingOffers.Query(), CancellationToken.None);

        Assert.Equal(["held-card"], offers.Value!.Select(o => o.Id));
    }

    [Theory]
    [InlineData(null, 5000, false)]
    [InlineData("0", 5000, false)]
    [InlineData("2000", 2000, false)]
    [InlineData("2000", 2000.01, true)]
    public async Task Cash_Jobs_Are_Hidden_Only_Above_A_Set_Cap(string? storedCap, double cashHeld, bool hidden)
    {
        var configuration = new Mock<IAppConfigurationProvider>();
        configuration
            .Setup(c => c.GetTenantSettingAsync("cash.float_cap", It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedCap);
        var cashLedger = new Mock<ICashLedgerRepository>();
        cashLedger
            .Setup(r => r.GetHeldAsync(Cleaner, "czk", It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal)cashHeld);
        var access = new OrderAccessService(
            Mock.Of<IUserSessionProvider>(),
            Mock.Of<IEmployeeRepository>(),
            Mock.Of<IOrderRepository>(),
            ValidatorTestHelpers.CurrencyResolver(),
            cashLedger.Object,
            configuration.Object);

        Assert.Equal(hidden, await access.CashJobsHiddenFromAsync(Cleaner, "czk", CancellationToken.None));
    }

    private static List<Order> Filter(Core.Domain.Specifications.OrderSpecification specification, params Order[] orders) =>
        orders.AsQueryable().Where(specification.SatisfiedBy()).ToList();

    private static Order HeldFor(Order order)
    {
        var currency = Core.Domain.Internationalization.Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = ValidatorTestHelpers.CurrencyId;
        order.SetCurrency(currency);
        order.GrantPreferredHold(Cleaner, DateTime.UtcNow.AddHours(2), DateTime.UtcNow, BookingPolicy.MaxPreferredOfferRounds);
        return order;
    }

    private static Order NewOrder(string id, PaymentType paymentType, string? assignedEmployeeId = null)
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            id,
            OrderStatus.New,
            maxEmployees: 2,
            paymentType: paymentType,
            paymentStatus: paymentType == PaymentType.Card ? PaymentStatus.Paid : PaymentStatus.Pending);

        if (assignedEmployeeId is not null)
        {
            var user = User.CreateWithPassword(
                $"{assignedEmployeeId}@cleansia.test", "Test-password-1!", "Ida", "Assigned", UserProfile.Employee);
            var employee = Employee.CreateWithUser(user);
            employee.Id = assignedEmployeeId;
            order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
        }

        return order;
    }
}
