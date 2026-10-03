using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Common;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-10-03: the customer's order detail states the free-cancellation window of THIS order —
/// the one frozen on it at booking, or its Plus window while the customer is an entitled member — resolved
/// as the cancel resolves it, so the note the customer reads and the fee a cancel applies cannot disagree.
/// No other caller is told a customer's entitlement.
/// </summary>
public sealed class OrderDetailFreeCancellationHoursTests
{
    private const string OrderId = "order-detail-free-cancel";
    private const string UserId = "user-detail-free-cancel";

    private static readonly CancellationTerms FrozenAtBooking = new(
        FreeHours: 48, PartialHours: 12, PartialFeeRate: 0.5m, LastMinuteFeeRate: 1m, PlusFreeHours: 6);

    private readonly Mock<IUserMembershipRepository> _memberships = new();

    [Fact]
    public async Task A_Customer_Reads_The_Window_Frozen_On_The_Order()
    {
        var detail = await ReadDetailAs(UserProfile.Customer);

        Assert.Equal(48, detail.FreeCancellationHours);
    }

    [Fact]
    public async Task An_Entitled_Plus_Member_Reads_The_Orders_Plus_Window()
    {
        _memberships
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMembership.Create(
                userId: UserId,
                membershipPlanId: "plan-plus",
                currencyId: "currency-czk",
                stripeSubscriptionId: "sub_free_cancel",
                currentPeriodStart: DateTime.UtcNow.AddDays(-1),
                currentPeriodEnd: DateTime.UtcNow.AddMonths(1)));

        var detail = await ReadDetailAs(UserProfile.Customer);

        Assert.Equal(6, detail.FreeCancellationHours);
    }

    [Theory]
    [InlineData(UserProfile.Employee)]
    [InlineData(UserProfile.Administrator)]
    public async Task No_Other_Caller_Reads_The_Customers_Window(UserProfile role)
    {
        var detail = await ReadDetailAs(role);

        Assert.Null(detail.FreeCancellationHours);
    }

    private async Task<OrderItem> ReadDetailAs(UserProfile role)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Hlavní 2", "Praha", "11000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: UserId,
            cancellationTerms: FrozenAtBooking);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));

        var access = new Mock<IOrderAccessService>();
        access.Setup(a => a.LoadOrderForCallerAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        access.Setup(a => a.CanBrowseOrderAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        access.Setup(a => a.CanAccessOrderAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        access.Setup(a => a.IsCustomerCaller()).Returns(role == UserProfile.Customer);
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(role == UserProfile.Customer ? UserId : "someone-on-staff");
        session
            .Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, role.ToString()));
        var acceptances = new Mock<IWorkContractAcceptanceRepository>();
        acceptances
            .Setup(r => r.GetForSeatsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new GetOrderDetails.Handler(
            access.Object,
            session.Object,
            Mock.Of<IEmployeePayConfigRepository>(),
            Mock.Of<IOrderEmployeePayRepository>(),
            Mock.Of<IOrderPhotoRepository>(),
            Mock.Of<IEmployeeRepository>(),
            Mock.Of<IUserRepository>(),
            Mock.Of<ITenantRepository>(),
            ExpressWaiverMocks.NoConsumer().Object,
            _memberships.Object,
            acceptances.Object,
            Mock.Of<IEmployeeActionAuditRepository>(),
            new CancellationPolicyResolver(_memberships.Object, Mock.Of<IOrderRepository>()));

        var result = await handler.Handle(new GetOrderDetails.Query(OrderId), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }
}
