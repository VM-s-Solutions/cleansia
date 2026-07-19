using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.LiveActivities;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.LiveActivities;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.LiveActivities;

/// <summary>
/// LA-1 / TC-LA-5 — the customer-mobile register surface (ADR-0029 D3). Registration UPSERTS on
/// (user, device, order) so a rotated ActivityKit token replaces the row rather than duplicating it;
/// <c>UserId</c> is taken from the session, never the body (S1); a foreign order is rejected as
/// OrderNotFound in the VALIDATOR (S3, indistinguishable from a missing order); a terminal-status order
/// is rejected. The push-to-start token (null OrderId) registers with no order ownership check.
/// </summary>
public class RegisterLiveActivityTokenTests
{
    private const string CallerUserId = "user-la-1";
    private const string OtherUserId = "user-la-2";
    private const string OrderId = "order-la-1";
    private const string DeviceId = "device-la-1";
    private const string Token = "apns-token-abc";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ILiveActivityTokenRepository> _tokenRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public RegisterLiveActivityTokenTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CallerUserId);
    }

    // ── Validator ────────────────────────────────────────────────────────────────────────────

    private IValidator<RegisterLiveActivityToken.Command> CreateValidator()
    {
        var validatorType = typeof(RegisterLiveActivityToken).GetNestedType("Validator", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(validatorType);
        return (IValidator<RegisterLiveActivityToken.Command>)Activator.CreateInstance(
            validatorType!, _orderRepository.Object, _session.Object)!;
    }

    private void ArrangeOrder(string ownerUserId, OrderStatus currentStatus)
    {
        var order = BuildOrder(ownerUserId, currentStatus);
        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
    }

    [Fact]
    public async Task Foreign_Order_Is_Rejected_As_OrderNotFound()
    {
        ArrangeOrder(OtherUserId, OrderStatus.Confirmed);

        var result = await CreateValidator().ValidateAsync(new RegisterLiveActivityToken.Command(DeviceId, Token, OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderNotFound);
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task Non_Registerable_Status_Is_Rejected(OrderStatus status)
    {
        ArrangeOrder(CallerUserId, status);

        var result = await CreateValidator().ValidateAsync(new RegisterLiveActivityToken.Command(DeviceId, Token, OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.LiveActivityOrderNotActive);
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.OnTheWay)]
    [InlineData(OrderStatus.InProgress)]
    public async Task Owned_Registerable_Order_Passes(OrderStatus status)
    {
        ArrangeOrder(CallerUserId, status);

        var result = await CreateValidator().ValidateAsync(new RegisterLiveActivityToken.Command(DeviceId, Token, OrderId));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task PushToStart_Token_With_No_Order_Passes_Without_An_Order_Check()
    {
        // No order lookup should be needed for the per-install push-to-start token.
        var result = await CreateValidator().ValidateAsync(new RegisterLiveActivityToken.Command(DeviceId, Token, OrderId: null));

        Assert.True(result.IsValid);
        _orderRepository.Verify(r => r.GetQueryable(), Times.Never);
    }

    [Fact]
    public async Task DeviceId_And_Token_Are_Required()
    {
        var result = await CreateValidator().ValidateAsync(new RegisterLiveActivityToken.Command(string.Empty, string.Empty, OrderId: null));

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count(e => e.ErrorMessage == BusinessErrorMessage.Required));
    }

    // ── Handler ──────────────────────────────────────────────────────────────────────────────

    private async Task<BusinessResult<RegisterLiveActivityToken.Response>> InvokeHandler(RegisterLiveActivityToken.Command command)
    {
        var handlerType = typeof(RegisterLiveActivityToken).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(handlerType!, _tokenRepository.Object, _session.Object)!;
        var handleMethod = handlerType!.GetMethod("Handle");
        Assert.NotNull(handleMethod);
        var task = (Task<BusinessResult<RegisterLiveActivityToken.Response>>)handleMethod!.Invoke(
            handler, [command, CancellationToken.None])!;
        return await task;
    }

    [Fact]
    public async Task Rotation_Upserts_The_Existing_Row_And_Never_Inserts()
    {
        var existing = LiveActivityToken.Create(CallerUserId, DeviceId, OrderId, "old-token", tenantId: null);
        _tokenRepository
            .Setup(r => r.GetByUserDeviceOrderAsync(CallerUserId, DeviceId, OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await InvokeHandler(new RegisterLiveActivityToken.Command(DeviceId, "new-token", OrderId));

        Assert.True(result.IsSuccess);
        Assert.Equal(existing.Id, result.Value.Id);
        Assert.Equal("new-token", existing.Token);
        _tokenRepository.Verify(r => r.Add(It.IsAny<LiveActivityToken>()), Times.Never);
    }

    [Fact]
    public async Task First_Registration_Adds_A_Row_Owned_By_The_Session_User()
    {
        _tokenRepository
            .Setup(r => r.GetByUserDeviceOrderAsync(CallerUserId, DeviceId, OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveActivityToken?)null);
        LiveActivityToken? added = null;
        _tokenRepository.Setup(r => r.Add(It.IsAny<LiveActivityToken>()))
            .Callback<LiveActivityToken>(t => added = t);

        var result = await InvokeHandler(new RegisterLiveActivityToken.Command(DeviceId, Token, OrderId));

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        // S1: the owner is the caller's session id, structurally impossible to override from the body.
        Assert.Equal(CallerUserId, added!.UserId);
        Assert.Equal(OrderId, added.OrderId);
        Assert.Equal(Token, added.Token);
    }

    [Fact]
    public async Task Missing_Session_Returns_UserNotFound()
    {
        _session.Setup(s => s.GetUserId()).Returns((string?)null);

        var result = await InvokeHandler(new RegisterLiveActivityToken.Command(DeviceId, Token, OrderId));

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.UserNotFound, result.Error!.Message);
        _tokenRepository.Verify(r => r.Add(It.IsAny<LiveActivityToken>()), Times.Never);
    }

    private static Order BuildOrder(string ownerUserId, OrderStatus currentStatus)
    {
        var address = Address.Create("123 Main St", "Prague", "11000", "cz");
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "test@example.com",
            customerPhone: "+420000000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: ownerUserId);
        order.Id = OrderId;

        var now = DateTimeOffset.UtcNow;
        var initial = OrderStatusTrack.Create(OrderStatus.New, order);
        initial.Created("test", now.AddMinutes(-10));
        order.AddOrderStatus(initial);

        if (currentStatus != OrderStatus.New)
        {
            var latest = OrderStatusTrack.Create(currentStatus, order);
            latest.Created("test", now);
            order.AddOrderStatus(latest);
        }

        return order;
    }
}
