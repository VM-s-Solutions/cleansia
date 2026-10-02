using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.TestUtilities;
using Cleansia.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// One order, one language: the Confirmed, Started and Completed e-mails are written in the language
/// the booking was made in, as the booking confirmation and the receipt already are — and fall back to
/// the account's stored preference only for an order no booking request made (a recurring occurrence).
///
/// <para>Before this the three status e-mails read the account's preference alone, so a customer whose
/// stored preference had gone stale got a Czech confirmation followed by English status e-mails.</para>
/// </summary>
public class OrderStatusEmailLanguageTests
{
    private const string OrderId = "order-email-language";
    private const string TakerId = "emp-email-language";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();
    private readonly Mock<ILiveActivityProducer> _liveActivityProducer = new();
    private readonly Mock<IEmailService> _emailService = new();

    private string? _emailLanguage;

    public OrderStatusEmailLanguageTests()
    {
        _emailService
            .Setup(s => s.SendOrderStatusUpdateEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<decimal?>(), It.IsAny<string?>()))
            .Callback<string, Order, string, string, CancellationToken, decimal?, string?>(
                (_, _, _, language, _, _, _) => _emailLanguage = language)
            .ReturnsAsync("message-id");
    }

    [Theory]
    [InlineData("cs", "en", "cs")]
    [InlineData(null, "sk", "sk")]
    [InlineData("de", "cs", "en")]
    public async Task The_Confirmed_Email_Is_In_The_Booking_Language(
        string? bookedIn, string accountLanguage, string expected)
    {
        var order = InLanguage(
            ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New, maxEmployees: 1),
            bookedIn, accountLanguage);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetLiveReservationsForBeneficiaryInWindowAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _employeeRepository
            .Setup(r => r.GetByIdAsync(TakerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidatorTestHelpers.BuildEmployee(TakerId, ContractStatus.Approved));
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TakerId);

        var handler = new TakeOrder.Handler(
            _orderRepository.Object,
            _employeeRepository.Object,
            _accessService.Object,
            _notificationProducer.Object,
            _emailService.Object,
            TestGuestOrderAccessTokenIssuer.WithNoLiveTokens(),
            new Mock<IWorkContractAcceptor>().Object,
            NullLogger<TakeOrder.Handler>.Instance);
        var result = await handler.Handle(
            new TakeOrder.Command(OrderId, WorkContractTestData.TextIdEn), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, _emailLanguage);
    }

    [Theory]
    [InlineData("cs", "en", "cs")]
    [InlineData(null, "sk", "sk")]
    [InlineData("de", "cs", "en")]
    public async Task The_Started_Email_Is_In_The_Booking_Language(
        string? bookedIn, string accountLanguage, string expected)
    {
        var order = InLanguage(
            ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.Confirmed, TakerId), bookedIn, accountLanguage);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

        var handler = new StartOrder.Handler(
            _orderRepository.Object,
            _emailService.Object,
            TestGuestOrderAccessTokenIssuer.WithNoLiveTokens(),
            _notificationProducer.Object,
            _liveActivityProducer.Object,
            NullLogger<StartOrder.Handler>.Instance);
        var result = await handler.Handle(new StartOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, _emailLanguage);
    }

    [Theory]
    [InlineData("cs", "en", "cs")]
    [InlineData(null, "sk", "sk")]
    [InlineData("de", "cs", "en")]
    public async Task The_Completed_Email_Is_In_The_Booking_Language(
        string? bookedIn, string accountLanguage, string expected)
    {
        var order = InLanguage(
            ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.InProgress, TakerId), bookedIn, accountLanguage);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

        var handler = new CompleteOrder.Handler(
            _orderRepository.Object,
            new Mock<IPendingDispatch>().Object,
            _notificationProducer.Object,
            _liveActivityProducer.Object,
            _emailService.Object,
            TestGuestOrderAccessTokenIssuer.WithNoLiveTokens(),
            new Mock<ILoyaltyService>().Object,
            new Mock<IReferralService>().Object,
            NullLogger<CompleteOrder.Handler>.Instance);
        var result = await handler.Handle(
            new CompleteOrder.Command(OrderId, ActualCompletionTimeMinutes: 120), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, _emailLanguage);
    }

    /// <summary>
    /// The booking's own language (null for an occurrence no request made) and the account's stored
    /// preference, which deliberately disagree. A booking language no e-mail copy exists in resolves to
    /// English, exactly as the same order's booking confirmation does, so the order still speaks one
    /// language.
    /// </summary>
    private static Order InLanguage(Order order, string? bookedIn, string accountLanguage)
    {
        order.SetLanguage(bookedIn);
        var account = User.CreateWithPassword(
            "customer@example.com", "Secret-123", "Jana", "Nováková", languageCode: accountLanguage);
        typeof(Order).GetProperty(nameof(Order.User))!.SetValue(order, account);
        return order;
    }
}
