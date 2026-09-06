using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Who gets woken when a seat comes free.
///
/// <para>Before <c>RequestCover</c> and <c>DropOrder</c> nothing had ever released a seat, so the only
/// road from a free seat to a cleaner was the hourly digest — and a job dropped ninety minutes before
/// its slot reached nobody in time. This is the accelerant, and the tests are mostly about who it must
/// NOT wake: a targeted push is a tap on the shoulder, and a cleaner tapped about work they cannot take
/// learns to ignore the next one.</para>
/// </summary>
public class SeatOpenedNotifierTests
{
    private const string CountryId = "country-cz";
    private const string OtherCountryId = "country-sk";
    private const string LeaverId = "emp-leaver";

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<INotificationProducer> _notifications = new();

    private static Employee Cleaner(
        string id,
        string countryId = CountryId,
        ContractStatus status = ContractStatus.Approved,
        int? radiusKm = null)
    {
        var employee = ValidatorTestHelpers.BuildEmployee(id, status);
        employee.AssignWorkCountry(countryId);
        if (radiusKm is not null)
        {
            employee.SetJobRadius(radiusKm);
        }

        return employee;
    }

    private void Arrange(params Employee[] cleaners) =>
        _employees.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(cleaners.AsQueryable().BuildMock());

    private static Order OrderInCountry()
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            "order-seat-1", OrderStatus.Confirmed, maxEmployees: 2);
        order.CustomerAddress!.Update("123 Main St", "Prague", "11000", CountryId);
        return order;
    }

    private Task Notify(Order order) =>
        SeatOpenedNotifier.NotifySeatOpenAsync(
            order, LeaverId, "assignment-1", _employees.Object, _notifications.Object, default);

    private void VerifyWoken(string employeeUserId, Times times) =>
        _notifications.Verify(n => n.NotifyAsync(
            employeeUserId,
            NotificationEventCatalog.OrderSeatOpen,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task ACleanerInTheOrdersCountryIsWoken()
    {
        var cleaner = Cleaner("emp-1");
        Arrange(cleaner);

        await Notify(OrderInCountry());

        VerifyWoken(cleaner.UserId!, Times.Once());
    }

    /// <summary>Jurisdiction, not preference: a cleaner cannot work a country they are not approved in.</summary>
    [Fact]
    public async Task ACleanerInAnotherCountryIsNotWoken()
    {
        var cleaner = Cleaner("emp-1", countryId: OtherCountryId);
        Arrange(cleaner);

        await Notify(OrderInCountry());

        VerifyWoken(cleaner.UserId!, Times.Never());
    }

    /// <summary>
    /// The cleaner who just walked away from this job must not be told it is available. Obvious, and
    /// the exact shape of push nobody forgives.
    /// </summary>
    [Fact]
    public async Task TheCleanerWhoLeftIsNotWoken()
    {
        var leaver = Cleaner(LeaverId);
        Arrange(leaver);

        await Notify(OrderInCountry());

        VerifyWoken(leaver.UserId!, Times.Never());
    }

    [Theory]
    [InlineData(ContractStatus.Pending)]
    [InlineData(ContractStatus.Rejected)]
    [InlineData(ContractStatus.Terminated)]
    public async Task ACleanerWithoutALiveContractIsNotWoken(ContractStatus status)
    {
        var cleaner = Cleaner("emp-1", status: status);
        Arrange(cleaner);

        await Notify(OrderInCountry());

        VerifyWoken(cleaner.UserId!, Times.Never());
    }

    /// <summary>
    /// A cleaner already on this job has nothing to take. Under a cover request the seat's holder is
    /// still assigned, so without this they would be pushed about their own job.
    /// </summary>
    [Fact]
    public async Task ACleanerAlreadyOnTheJobIsNotWoken()
    {
        var order = OrderInCountry();
        var cleaner = Cleaner("emp-1");
        order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
        Arrange(cleaner);

        await Notify(order);

        VerifyWoken(cleaner.UserId!, Times.Never());
    }

    /// <summary>
    /// The dedup subject is the ASSIGNMENT. One order opens a seat more than once over its life — a
    /// cover request answered, then the replacement dropping — and the bare order id would mint a key
    /// the same cleaner already holds, which the outbox's unique index raises at COMMIT, rolling the
    /// whole release back.
    /// </summary>
    [Fact]
    public async Task TheDedupSubjectNamesTheAssignmentNotTheOrder()
    {
        var order = OrderInCountry();
        Arrange(Cleaner("emp-1"));

        await Notify(order);

        _notifications.Verify(n => n.NotifyAsync(
            It.IsAny<string>(),
            NotificationEventCatalog.OrderSeatOpen,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(),
            $"{order.Id}:assignment-1",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The country term is a conjunction, not a hint: an order outside a cleaner's jurisdiction wakes
    /// nobody rather than everybody. Fails closed.
    /// </summary>
    [Fact]
    public async Task AnOrderOutsideTheCleanersJurisdictionWakesNobody()
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            "order-seat-2", OrderStatus.Confirmed, maxEmployees: 2);
        order.CustomerAddress!.Update("123 Main St", "Prague", "11000", OtherCountryId);
        Arrange(Cleaner("emp-1"));

        await Notify(order);

        _notifications.Verify(n => n.NotifyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
