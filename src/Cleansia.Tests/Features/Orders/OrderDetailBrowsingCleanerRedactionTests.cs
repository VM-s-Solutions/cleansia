using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Common;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The order LIST already withholds the customer's identity from a cleaner who has not taken the job
/// (<c>GetPagedOrders</c>); the DETAIL of the same order used to hand all of it back, plus the
/// detail-only fields the list has no room for — the door code, the free-text instructions, the
/// previous crew's phone numbers, the notes/issues/review. One extra GET undid the list's withholding.
///
/// <para>Every asserted-blank field is arranged NON-EMPTY here, and the assigned-cleaner mirror of each
/// case reads the same fixture through the same handler — so a fix of "blank it for everyone", and a
/// fixture that simply never set the field, both fail.</para>
/// </summary>
public class OrderDetailBrowsingCleanerRedactionTests
{
    private const string OrderId = "order-redaction-1";
    private const string AssignedEmployeeId = "employee-assigned-red";
    private const string BrowsingEmployeeId = "employee-browsing-red";
    private const string CustomerUserId = "user-customer-red";

    private const string CustomerName = "Jana Novakova";
    private const string CustomerEmail = "jana.novakova@example.test";
    private const string CustomerPhone = "+420777123456";
    private const string Street = "Vinohradska 12";
    private const string City = "Praha";
    private const string ZipCode = "12000";
    private const string ApproximateAddress = "Praha · 120 xx";
    private const double Latitude = 50.0755;
    private const double Longitude = 14.4378;
    private const string AccessInstructions = "Code 1234 at the gate.";
    private const string SpecialInstructions = "Use the eco products under the sink.";
    private const string CustomerNotes = "Cat is friendly.";
    private const string CompletionNotes = "Balcony door was jammed.";
    private const string ReceiptNumber = "CZ-2026-000123";
    private const string NoteContent = "Second bathroom needed a re-do.";
    private const string IssueDescription = "Broken tile behind the washing machine.";
    private const string ReviewComment = "Jana was great, the flat is spotless.";
    private const string CleanerFirstName = "Petra";
    private const string CleanerLastName = "Svobodova";
    private const string CleanerPhone = "+420602987654";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _orderAccessService = new();
    private readonly Mock<IUserSessionProvider> _userSessionProvider = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderEmployeePayRepository = new();
    private readonly Mock<IOrderPhotoRepository> _orderPhotoRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = ExpressWaiverMocks.NoConsumer();

    // ── The browsing cleaner: admitted by the loose gate, entitled to nothing about the customer ──

    [Fact]
    public async Task Browsing_Cleaner_Gets_No_Customer_Identity()
    {
        var detail = await BrowsingCleanerDetailAsync();

        Assert.Equal(string.Empty, detail.CustomerName);
        Assert.Equal(string.Empty, detail.CustomerEmail);
        Assert.Equal(string.Empty, detail.CustomerPhone);
    }

    [Fact]
    public async Task Browsing_Cleaner_Gets_No_Address_And_No_Coordinates()
    {
        var detail = await BrowsingCleanerDetailAsync();

        Assert.Null(detail.Address);
    }

    /// <summary>
    /// The street goes, the zone stays. Blanking the address as a whole record took the coarse location
    /// with it and left the detail screen — the one a cleaner opens to decide whether the job is worth
    /// taking — saying nothing at all about where the work is, while the board row they tapped through
    /// still showed it.
    /// </summary>
    [Fact]
    public async Task Browsing_Cleaner_Still_Gets_The_Coarse_Location()
    {
        var detail = await BrowsingCleanerDetailAsync();

        Assert.Equal(ApproximateAddress, detail.CustomerAddressApproximate);
        Assert.Null(detail.Address);
    }

    /// <summary>
    /// The board row a cleaner tapped through already told them the crew size and whether a seat was
    /// free; the detail behind it said nothing, so the screen they actually tap Take on was the one
    /// screen that could not warn them. <c>TakeOrder</c>'s free-seat conjunct then refuses a take the
    /// detail gave no warning about.
    ///
    /// <para>Literals, not a read-back of the fixture: four of the five are numeric and one is a bool,
    /// so a mapper that stopped populating them would satisfy any assertion phrased as a comparison
    /// against a default-valued fixture.</para>
    /// </summary>
    [Fact]
    public async Task Browsing_Cleaner_Still_Gets_The_Crew_Size_And_Free_Seats()
    {
        var detail = await BrowsingCleanerDetailAsync();

        Assert.Equal(2, detail.RequiredEmployees);
        Assert.Equal(2, detail.MaxEmployees);
        Assert.Equal(1, detail.AssignedEmployeesCount);
        Assert.Equal(1, detail.AvailableSpots);
        Assert.True(detail.HasAvailableSpots);
    }

    [Fact]
    public async Task Browsing_Cleaner_Gets_No_Access_Instructions()
    {
        var detail = await BrowsingCleanerDetailAsync();

        Assert.Null(detail.AccessInstructions);
    }

    [Fact]
    public async Task Browsing_Cleaner_Gets_No_Special_Instructions_Or_Customer_Notes()
    {
        var detail = await BrowsingCleanerDetailAsync();

        Assert.Null(detail.SpecialInstructions);
        Assert.Null(detail.Notes);
    }

    [Fact]
    public async Task Browsing_Cleaner_Gets_No_Completion_Notes()
    {
        var detail = await BrowsingCleanerDetailAsync();

        Assert.Null(detail.CompletionNotes);
    }

    [Fact]
    public async Task Browsing_Cleaner_Gets_No_Confirmation_Code()
    {
        var detail = await BrowsingCleanerDetailAsync();

        Assert.Equal(string.Empty, detail.ConfirmationCode);
    }

    [Fact]
    public async Task Browsing_Cleaner_Gets_No_Receipt_Number()
    {
        var detail = await BrowsingCleanerDetailAsync();

        Assert.Null(detail.ReceiptNumber);
    }

    [Fact]
    public async Task Browsing_Cleaner_Gets_No_Surname_Or_Phone_Of_The_Crew()
    {
        var detail = await BrowsingCleanerDetailAsync();

        var crewMember = Assert.Single(detail.AssignedEmployees);
        Assert.Equal(CleanerFirstName, crewMember.FullName);
        Assert.Null(crewMember.PhoneNumber);
    }

    [Fact]
    public async Task Browsing_Cleaner_Gets_No_Notes_Issues_Or_Review()
    {
        var detail = await BrowsingCleanerDetailAsync();

        Assert.Empty(detail.OrderNotes);
        Assert.Empty(detail.OrderIssues);
        Assert.Null(detail.Review);
    }

    /// <summary>
    /// The browse branch exists so a cleaner can judge the job before tapping Take. Redaction that took
    /// the job description with the PII would close the branch by accident.
    /// </summary>
    [Fact]
    public async Task Browsing_Cleaner_Still_Gets_What_The_Take_Decision_Needs()
    {
        var order = BuildFullyPopulatedOrder();
        var detail = await BrowsingCleanerDetailAsync(order);

        Assert.Equal(OrderId, detail.Id);
        Assert.Equal(order.DisplayOrderNumber, detail.DisplayOrderNumber);
        Assert.Equal(order.CleaningDateTime, detail.CleaningDateTime);
        Assert.Equal(3, detail.Rooms);
        Assert.Equal(2, detail.Bathrooms);
        Assert.True(detail.Extras["insideOven"]);
        Assert.Equal(180, detail.EstimatedTime);
        Assert.Equal(1500m, detail.TotalPrice);
        Assert.Equal((int)PaymentType.Card, detail.PaymentType.Value);
        Assert.Equal((int)PaymentStatus.Paid, detail.PaymentStatus.Value);
        Assert.Equal((int)OrderStatus.Completed, detail.OrderStatus.Value);
        Assert.NotEmpty(detail.StatusHistory);
        Assert.False(detail.IsAssignedToCurrentUser);
    }

    // ── The mirror: the cleaner who took the job reads the same fixture in full ──

    [Fact]
    public async Task Assigned_Cleaner_Still_Gets_Every_Redacted_Field()
    {
        var detail = await AssignedCleanerDetailAsync();

        Assert.Equal(CustomerName, detail.CustomerName);
        Assert.Equal(CustomerEmail, detail.CustomerEmail);
        Assert.Equal(CustomerPhone, detail.CustomerPhone);
        Assert.NotNull(detail.Address);
        Assert.Equal(Street, detail.Address!.Street);
        Assert.Equal(City, detail.Address.City);
        Assert.Equal(ZipCode, detail.Address.ZipCode);
        Assert.Equal(Latitude, detail.Address.Latitude);
        Assert.Equal(Longitude, detail.Address.Longitude);
        Assert.Equal(ApproximateAddress, detail.CustomerAddressApproximate);
        Assert.Equal(AccessInstructions, detail.AccessInstructions);
        Assert.Equal(SpecialInstructions, detail.SpecialInstructions);
        Assert.Equal(CustomerNotes, detail.Notes);
        Assert.Equal(CompletionNotes, detail.CompletionNotes);
        Assert.NotEmpty(detail.ConfirmationCode);
        Assert.Equal(ReceiptNumber, detail.ReceiptNumber);
        Assert.Single(detail.OrderNotes, n => n.Content == NoteContent);
        Assert.Single(detail.OrderIssues, i => i.Description == IssueDescription);
        Assert.Equal(ReviewComment, detail.Review!.Comment);

        var crewMember = Assert.Single(detail.AssignedEmployees);
        Assert.Equal($"{CleanerFirstName} {CleanerLastName}", crewMember.FullName);
        Assert.Equal(CleanerPhone, crewMember.PhoneNumber);
        Assert.True(detail.IsAssignedToCurrentUser);
    }

    // ── The other two audiences reach the handler through the strict gate and are untouched ──

    [Fact]
    public async Task The_Orders_Own_Customer_Still_Gets_Everything()
    {
        var order = BuildFullyPopulatedOrder();
        ArrangeCommon(order);
        ArrangeCaller(order, UserProfile.Customer, callerEmployeeId: null, isEntitled: true, isCustomer: true);

        var result = await CreateHandler().Handle(new GetOrderDetails.Query(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var detail = result.Value!;
        Assert.Equal(CustomerName, detail.CustomerName);
        Assert.Equal(AccessInstructions, detail.AccessInstructions);
        Assert.NotNull(detail.Address);
        Assert.Equal(ApproximateAddress, detail.CustomerAddressApproximate);
        Assert.Equal(ReviewComment, detail.Review!.Comment);

        // The pre-existing customer-facing masking of the cleaner is unchanged.
        var crewMember = Assert.Single(detail.AssignedEmployees);
        Assert.Equal(CleanerFirstName, crewMember.FullName);
        Assert.Null(crewMember.PhoneNumber);
    }

    [Fact]
    public async Task The_Administrator_Gets_Everything_Except_The_Entry_Instructions()
    {
        var order = BuildFullyPopulatedOrder();
        ArrangeCommon(order);
        ArrangeCaller(order, UserProfile.Administrator, callerEmployeeId: null, isEntitled: true, isCustomer: false);

        var result = await CreateHandler().Handle(new GetOrderDetails.Query(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var detail = result.Value!;
        Assert.Equal(CustomerName, detail.CustomerName);
        Assert.Equal(CustomerPhone, detail.CustomerPhone);
        Assert.Equal(Latitude, detail.Address!.Latitude);
        Assert.Equal(ApproximateAddress, detail.CustomerAddressApproximate);
        Assert.Equal(ReceiptNumber, detail.ReceiptNumber);

        var crewMember = Assert.Single(detail.AssignedEmployees);
        Assert.Equal($"{CleanerFirstName} {CleanerLastName}", crewMember.FullName);
        Assert.Equal(CleanerPhone, crewMember.PhoneNumber);
    }

    /// <summary>
    /// G-11. Until 2026-08-14 this test asserted the admin "still gets everything", and the entry
    /// instructions rode along with it — free text of the form "key under the mat", reaching every admin
    /// who opened an order, with no reveal step and therefore no record of who looked.
    ///
    /// <para>They now come only from <c>RevealOrderAccessInstructions</c>, which is a Command so the
    /// audit engine writes a row. The flag stays true so the UI can tell "nothing to reveal" from
    /// "something to reveal" without holding the text.</para>
    /// </summary>
    [Fact]
    public async Task The_Administrator_Does_Not_Get_The_Entry_Instructions_Only_That_There_Are_Some()
    {
        var order = BuildFullyPopulatedOrder();
        ArrangeCommon(order);
        ArrangeCaller(order, UserProfile.Administrator, callerEmployeeId: null, isEntitled: true, isCustomer: false);

        var result = await CreateHandler().Handle(new GetOrderDetails.Query(OrderId), CancellationToken.None);

        var detail = result.Value!;
        Assert.Null(detail.AccessInstructions);
        Assert.True(detail.HasAccessInstructions);
    }

    /// <summary>The assigned cleaner is standing at the door; withholding from them would be the bug.</summary>
    [Fact]
    public async Task The_Assigned_Cleaner_Still_Gets_The_Entry_Instructions()
    {
        var order = BuildFullyPopulatedOrder();
        ArrangeCommon(order);
        ArrangeCaller(order, UserProfile.Employee, callerEmployeeId: AssignedEmployeeId, isEntitled: true, isCustomer: false);

        var result = await CreateHandler().Handle(new GetOrderDetails.Query(OrderId), CancellationToken.None);

        var detail = result.Value!;
        Assert.Equal(AccessInstructions, detail.AccessInstructions);
        Assert.True(detail.HasAccessInstructions);
    }

    // ── Arrangement ──

    private async Task<OrderItem> BrowsingCleanerDetailAsync(Order? seed = null)
    {
        var order = seed ?? BuildFullyPopulatedOrder();
        ArrangeCommon(order);
        ArrangeCaller(order, UserProfile.Employee, BrowsingEmployeeId, isEntitled: false, isCustomer: false);

        var result = await CreateHandler().Handle(new GetOrderDetails.Query(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private async Task<OrderItem> AssignedCleanerDetailAsync()
    {
        var order = BuildFullyPopulatedOrder();
        ArrangeCommon(order);
        ArrangeCaller(order, UserProfile.Employee, AssignedEmployeeId, isEntitled: true, isCustomer: false);

        var result = await CreateHandler().Handle(new GetOrderDetails.Query(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private GetOrderDetails.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            _orderAccessService.Object,
            _userSessionProvider.Object,
            _payConfigRepository.Object,
            _orderEmployeePayRepository.Object,
            _orderPhotoRepository.Object,
            _employeeRepository.Object,
            _expressWaiverConsumer.Object,
            Mock.Of<IUserMembershipRepository>());

    private void ArrangeCommon(Order order)
    {
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _orderPhotoRepository
            .Setup(r => r.GetPhotoCountByOrderIdAndTypeAsync(OrderId, PhotoType.After, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _orderEmployeePayRepository
            .Setup(r => r.GetByOrderAndEmployeeAsync(OrderId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderEmployeePay?)null);
        _payConfigRepository
            .Setup(r => r.GetServiceConfigsForOrderAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeePayConfig>());
        _payConfigRepository
            .Setup(r => r.GetPackageConfigsForOrderAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeePayConfig>());
    }

    private void ArrangeCaller(
        Order order, UserProfile role, string? callerEmployeeId, bool isEntitled, bool isCustomer)
    {
        _userSessionProvider
            .Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, role.ToString()));
        _userSessionProvider.Setup(s => s.GetUserId()).Returns(isCustomer ? CustomerUserId : "caller-user");
        _orderAccessService.Setup(a => a.IsCustomerCaller()).Returns(isCustomer);
        _orderAccessService
            .Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(callerEmployeeId);
        _orderAccessService
            .Setup(a => a.CanBrowseOrderAsync(order, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderAccessService
            .Setup(a => a.CanAccessOrderAsync(order, It.IsAny<CancellationToken>()))
            .ReturnsAsync(isEntitled);
    }

    /// <summary>
    /// Every field this suite asserts blank is set to a distinctive non-empty value here — the
    /// arrangement is the anti-vacuity control, and the assigned-cleaner mirror proves it reaches
    /// the mapper.
    /// </summary>
    private static Order BuildFullyPopulatedOrder()
    {
        var address = Address.Create(Street, City, ZipCode, "cz", latitude: Latitude, longitude: Longitude);

        var order = Order.Create(
            customerName: CustomerName,
            customerEmail: CustomerEmail,
            customerPhone: CustomerPhone,
            customerAddress: address,
            rooms: 3,
            bathrooms: 2,
            extras: new Dictionary<string, bool> { ["insideOven"] = true },
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerUserId,
            specialInstructions: SpecialInstructions,
            accessInstructions: AccessInstructions);

        order.Id = OrderId;
        order.SetCurrency(Currency.Create("CZK", "Kč", "Czech Koruna", 1m));
        order.UpdateEstimatedTime(180);

        // Two required seats, no spare, one of them filled below — so none of the five seat members
        // reads as its type default and a mapper that stopped populating them cannot pass.
        order.CalculateRequiredEmployees(BookingPolicy.SpareSeatsPerOrder);
        order.Created("test", DateTime.UtcNow.AddDays(-3));

        var opened = OrderStatusTrack.Create(OrderStatus.New, order);
        opened.Created("test", DateTimeOffset.UtcNow.AddDays(-3));
        order.AddOrderStatus(opened);
        var finished = OrderStatusTrack.Create(OrderStatus.Completed, order);
        finished.Created("test", DateTimeOffset.UtcNow.AddHours(-1));
        order.AddOrderStatus(finished);

        order.CompleteOrder(175, CompletionNotes);
        order.AddAssignedEmployee(OrderEmployee.Create(order, BuildCleaner()));
        order.AddNote(OrderNote.Create(OrderId, AssignedEmployeeId, NoteContent));
        order.AddIssue(OrderIssue.Create(OrderId, AssignedEmployeeId, IssueDescription));
        order.AddReview(OrderReview.Create(OrderId, CustomerUserId, 5, ReviewComment));

        // Notes and Receipt have no domain writer on this path — EF materializes both. Reflection is
        // the only way to arrange a non-empty value, and a blank one would make its test vacuous.
        typeof(Order).GetProperty(nameof(Order.Notes))!.SetValue(order, CustomerNotes);
        typeof(Order).GetProperty(nameof(Order.Receipt))!.SetValue(
            order, OrderReceipt.Create(OrderId, ReceiptNumber, "receipt.pdf", "blob", "en"));

        return order;
    }

    private static Employee BuildCleaner()
    {
        var user = User.CreateWithPassword(
            "petra@cleansia.test", "Test-password-1!", CleanerFirstName, CleanerLastName, UserProfile.Employee);
        user.Id = $"user-{AssignedEmployeeId}";
        user.UpdatePhoneNumber(CleanerPhone);

        var employee = Employee.CreateWithUser(user);
        employee.Id = AssignedEmployeeId;
        return employee;
    }
}
