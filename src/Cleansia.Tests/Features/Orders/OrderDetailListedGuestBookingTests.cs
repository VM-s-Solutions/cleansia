using Cleansia.Core.Blobs.Abstractions;
using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Common;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-10-26, "I select option B": a signed-in customer opens a booking made without an account that
/// their My orders lists under their e-mail. It opens in the listed shape — what the booking's e-mails carried, and
/// the crew as a customer sees it — and nothing else is admitted: an order the caller neither owns, nor is listed,
/// nor may browse is not found.
/// </summary>
public sealed class OrderDetailListedGuestBookingTests
{
    private const string OrderId = "order-listed-detail";
    private const string GuestEmail = "jana.novak@cleansia.test";

    private readonly Mock<IOrderAccessService> _access = new();
    private readonly Mock<IOrderPhotoRepository> _photos = new();

    [Fact]
    public async Task A_Listed_Guest_Booking_Opens_With_What_Its_Emails_Carried_And_The_Crews_Given_Names()
    {
        var order = GuestOrder();
        _access.Setup(a => a.IsListedGuestBooking(order)).Returns(true);

        var result = await Handler().Handle(new GetOrderDetails.Query(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var detail = result.Value!;
        Assert.True(detail.IsGuestBooking);
        Assert.Equal("Jana Nováková", detail.CustomerName);
        Assert.Equal(GuestEmail, detail.CustomerEmail);
        Assert.Equal("Vinohradská 12", detail.Address!.Street);
        Assert.Equal(BookingPolicy.CancellationTermsAtBooking.FreeHours, detail.FreeCancellationHours);
        Assert.Empty(detail.CustomerPhone);
        Assert.Null(detail.Notes);
        Assert.Null(detail.SpecialInstructions);
        Assert.Null(detail.AccessInstructions);
        Assert.Null(detail.CustomerFloor);
        Assert.Null(detail.CustomerApartment);
        Assert.Null(detail.ReceiptNumber);
        Assert.Null(detail.Review);
        Assert.False(detail.HasAfterPhotos);
        var crewMember = Assert.Single(detail.AssignedEmployees);
        Assert.Equal("Emp", crewMember.FullName);
        Assert.Null(crewMember.PhoneNumber);
        _photos.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task An_Order_The_Caller_Is_Not_Listed_For_And_May_Not_Browse_Is_Not_Found()
    {
        GuestOrder();

        var result = await Handler().Handle(new GetOrderDetails.Query(OrderId), CancellationToken.None);

        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
    }

    private Order GuestOrder()
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: GuestEmail,
            customerPhone: "+420777000111",
            customerAddress: Address.Create("Vinohradská 12", "Praha", "12000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: null,
            specialInstructions: "Eco products only.",
            accessInstructions: "Code 1234 at the gate.",
            customerFloor: "3",
            customerApartment: "12",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        order.AddAssignedEmployee(OrderEmployee.Create(
            order, ValidatorTestHelpers.BuildEmployee("emp-listed-detail", ContractStatus.Approved)));

        _access.Setup(a => a.LoadListedOrderForCallerAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _access.Setup(a => a.IsCustomerCaller()).Returns(true);
        return order;
    }

    private GetOrderDetails.Handler Handler()
    {
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns("user-listed-detail");
        session.Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, UserProfile.Customer.ToString()));
        var memberships = new Mock<IUserMembershipRepository>();
        return new GetOrderDetails.Handler(
            _access.Object,
            session.Object,
            Mock.Of<IEmployeePayConfigRepository>(),
            Mock.Of<IOrderEmployeePayRepository>(),
            _photos.Object,
            Mock.Of<IEmployeeRepository>(),
            Mock.Of<IUserRepository>(),
            Mock.Of<ITenantRepository>(),
            ExpressWaiverMocks.NoConsumer().Object,
            memberships.Object,
            Mock.Of<IWorkContractAcceptanceRepository>(),
            Mock.Of<IEmployeeActionAuditRepository>(),
            new CancellationPolicyResolver(memberships.Object, Mock.Of<IOrderRepository>()),
            Mock.Of<IAppConfigurationProvider>(),
            Mock.Of<IBlobContainerClientFactory>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GetOrderDetails.Handler>.Instance);
    }
}
