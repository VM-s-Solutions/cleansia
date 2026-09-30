using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner rulings 2026-09-28. The administrator's order detail states the cash handover — when, which
/// cleaner, how much — and nobody else's does. And every caller reads whether a recurring
/// occurrence still needs the customer's confirmation from the server, because a confirmed
/// cash occurrence stays unpaid and the payment status can no longer say it.
/// </summary>
public class OrderDetailCashAndConfirmationFieldsTests
{
    private const string OrderId = "order-detail-cash-1";
    private const string UserId = "user-detail-cash-1";
    private const string CleanerId = "emp-detail-cash-1";

    [Fact]
    public async Task The_Admin_Detail_States_When_Who_And_How_Much_Cash_Was_Taken()
    {
        var order = CashOrder(recurringTemplateId: null);
        var collectedAt = new DateTime(2026, 9, 28, 10, 5, 0, DateTimeKind.Utc);
        order.MarkCashCollected(CleanerId, collectedAt);

        var detail = await ReadDetailAs(order, UserProfile.Administrator);

        Assert.Equal(collectedAt, detail.CashCollectedAt);
        Assert.Equal("Petra Svobodova", detail.CashCollectedByName);
        Assert.Equal(1000m, detail.CashCollectedAmount);
    }

    [Fact]
    public async Task A_Customer_Reading_Their_Own_Order_Gets_No_Cash_Handover_Fields()
    {
        var order = CashOrder(recurringTemplateId: null);
        order.MarkCashCollected(CleanerId);

        var detail = await ReadDetailAs(order, UserProfile.Customer);

        Assert.Null(detail.CashCollectedAt);
        Assert.Null(detail.CashCollectedByName);
        Assert.Null(detail.CashCollectedAmount);
    }

    [Fact]
    public async Task A_Cash_Occurrence_Needs_Confirmation_Until_Confirmed_And_Then_Not_While_Still_Unpaid()
    {
        var order = CashOrder(recurringTemplateId: "tmpl-weekly");

        Assert.True((await ReadDetailAs(order, UserProfile.Customer)).NeedsConfirmation);

        order.ConfirmByCustomer(DateTime.UtcNow);
        var confirmed = await ReadDetailAs(order, UserProfile.Customer);

        Assert.False(confirmed.NeedsConfirmation);
        Assert.Equal(nameof(PaymentStatus.Pending), confirmed.PaymentStatus.Name);
    }

    [Fact]
    public async Task A_One_Off_Order_Never_Needs_Confirmation()
    {
        var detail = await ReadDetailAs(CashOrder(recurringTemplateId: null), UserProfile.Customer);

        Assert.False(detail.NeedsConfirmation);
    }

    private static Order CashOrder(string? recurringTemplateId)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Hlavní 2", "Praha", "11000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Pending,
            userId: UserId,
            recurringTemplateId: recurringTemplateId);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }

    private static async Task<OrderItem> ReadDetailAs(Order order, UserProfile role)
    {
        var access = new Mock<IOrderAccessService>();
        access.Setup(a => a.LoadOrderForCallerAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        access.Setup(a => a.CanBrowseOrderAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        access.Setup(a => a.CanAccessOrderAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        access.Setup(a => a.IsCustomerCaller()).Returns(role == UserProfile.Customer);
        var session = new Mock<IUserSessionProvider>();
        session
            .Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, role.ToString()));
        var acceptances = new Mock<IWorkContractAcceptanceRepository>();
        acceptances
            .Setup(r => r.GetForSeatsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var cleanerUser = User.CreateWithPassword("petra@cleansia.test", "Passw0rd!", "Petra", "Svobodova");
        var cleaner = Employee.CreateWithUser(cleanerUser);
        cleaner.Id = CleanerId;
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetQueryableIgnoringTenant()).Returns(new[] { cleaner }.AsQueryable().BuildMock());

        var handler = new GetOrderDetails.Handler(
            access.Object,
            session.Object,
            Mock.Of<IEmployeePayConfigRepository>(),
            Mock.Of<IOrderEmployeePayRepository>(),
            Mock.Of<IOrderPhotoRepository>(),
            employees.Object,
            Mock.Of<IUserRepository>(),
            Mock.Of<ITenantRepository>(),
            ExpressWaiverMocks.NoConsumer().Object,
            Mock.Of<IUserMembershipRepository>(),
            acceptances.Object,
            Mock.Of<IEmployeeActionAuditRepository>());

        var result = await handler.Handle(new GetOrderDetails.Query(OrderId), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }
}
