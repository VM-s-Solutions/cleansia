using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;
using Cleansia.TestUtilities;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A cleaner is paid in the currency of the country they work in (owner ruling 2026-09-12), so an order
/// priced in any other currency is not theirs to take, open or answer. The rule lives in ONE place —
/// <see cref="OrderVisibility.PayableTo(Order, string?, string?)"/>, conjoined into
/// <c>OrderVisibility.OpenTo</c> — and these cases prove the three cleaner-side gates that are not a
/// board all read it: the take gate, the browse gate and the pending-offer list.
///
/// <para>Each refusal has its admitted twin (same order, the cleaner paid in its currency), because a
/// gate that refuses everything passes a refusal-only suite.</para>
/// </summary>
public class CleanerCurrencyGateTests
{
    private const string OrderId = "order-currency-gate";
    private const string CallerUserId = "user-currency-caller";
    private const string CallerEmployeeId = "emp-currency-caller";
    private const string Eur = "eur";

    // ── TakeOrder ──

    /// <summary>
    /// The refusal is the one a missing order returns: the order was never on this cleaner's board, so
    /// from their side it does not exist — and it is ONE error, as every take refusal is.
    /// </summary>
    [Fact]
    public async Task Taking_An_Order_In_Another_Currency_Is_Refused_As_Not_Found()
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New, maxEmployees: 2);

        var result = await TakeValidator(paidIn: Eur).ValidateAsync(new TakeOrder.Command(OrderId, WorkContractTestData.TextIdEn));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Taking_An_Order_In_The_Cleaners_Own_Currency_Passes_The_Gate()
    {
        var result = await TakeValidator(paidIn: ValidatorTestHelpers.CurrencyId)
            .ValidateAsync(new TakeOrder.Command(OrderId, WorkContractTestData.TextIdEn));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    // ── CanBrowseOrderAsync ──

    [Fact]
    public async Task An_Order_In_Another_Currency_Is_Not_Browsable()
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            OrderId, OrderStatus.Confirmed, paymentType: PaymentType.Card, paymentStatus: PaymentStatus.Paid);

        Assert.False(await BrowseGate(paidIn: Eur).CanBrowseOrderAsync(order, CancellationToken.None));
        Assert.True(await BrowseGate(paidIn: ValidatorTestHelpers.CurrencyId).CanBrowseOrderAsync(order, CancellationToken.None));
    }

    /// <summary>
    /// The admin override, seen from the cleaner it was applied to: an assigned cleaner is admitted by
    /// <c>CanAccessOrderAsync</c> before the browse branch and its currency term are ever reached.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_Assigned_By_An_Admin_Still_Opens_An_Order_In_Another_Currency()
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.Confirmed, CallerEmployeeId, PaymentType.Card, PaymentStatus.Paid);

        Assert.True(await BrowseGate(paidIn: Eur).CanBrowseOrderAsync(order, CancellationToken.None));
    }

    // ── GetMyPendingOffers ──

    [Fact]
    public async Task A_Hold_On_An_Order_In_Another_Currency_Is_Not_A_Pending_Offer()
    {
        var held = HeldFor(CallerEmployeeId);

        var inEur = await PendingOffers(paidIn: Eur, held);
        var inCzk = await PendingOffers(paidIn: ValidatorTestHelpers.CurrencyId, held);

        Assert.True(inEur.IsSuccess);
        Assert.Empty(inEur.Value!);
        Assert.Equal(OrderId, Assert.Single(inCzk.Value!).Id);
    }

    // ── arrangement ──

    private static TakeOrder.Validator TakeValidator(string paidIn)
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New, maxEmployees: 2);
        var caller = ValidatorTestHelpers.BuildEmployee(CallerEmployeeId, ContractStatus.Approved, withAddress: true);

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        orderRepository
            .Setup(r => r.GetEmployeeOrderCountThisWeekAsync(CallerEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        orderRepository
            .Setup(r => r.HasOverlappingOrderAsync(
                CallerEmployeeId, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(CallerEmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        employeeRepository.Setup(r => r.GetQueryable()).Returns(new[] { caller }.AsQueryable().BuildMock());

        return new TakeOrder.Validator(
            orderRepository.Object,
            employeeRepository.Object,
            AccessService().Object,
            ValidatorTestHelpers.CurrencyResolver(paidIn),
            WorkContractTestData.LegalDocumentRepository().Object);
    }

    private static OrderAccessService BrowseGate(string paidIn)
    {
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(CallerUserId);
        session.Setup(s => s.GetEmployeeId()).Returns(CallerEmployeeId);
        session
            .Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()));

        return new OrderAccessService(
            session.Object, new Mock<IEmployeeRepository>().Object, Mock.Of<IOrderRepository>(), ValidatorTestHelpers.CurrencyResolver(paidIn));
    }

    private static Task<Infra.Common.Validations.BusinessResult<IReadOnlyList<Core.AppServices.Features.Orders.DTOs.PendingOfferItem>>>
        PendingOffers(string paidIn, Order held)
    {
        var orderRepository = new Mock<IOrderRepository>();
        orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { held }.AsQueryable().BuildMock());

        var handler = new GetMyPendingOffers.Handler(
            orderRepository.Object, AccessService().Object, ValidatorTestHelpers.CurrencyResolver(paidIn));

        return handler.Handle(new GetMyPendingOffers.Query(), CancellationToken.None);
    }

    private static Mock<IOrderAccessService> AccessService()
    {
        var accessService = new Mock<IOrderAccessService>();
        accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CallerEmployeeId);
        return accessService;
    }

    private static Order HeldFor(string employeeId)
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            OrderId, OrderStatus.New, maxEmployees: 1, paymentType: PaymentType.Card, paymentStatus: PaymentStatus.Paid);
        var currency = Core.Domain.Internationalization.Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = ValidatorTestHelpers.CurrencyId;
        order.SetCurrency(currency);
        order.GrantPreferredHold(
            employeeId, DateTime.UtcNow.AddHours(2), DateTime.UtcNow, BookingPolicy.MaxPreferredOfferRounds);
        return order;
    }
}
