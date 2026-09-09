using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// ADR-0001 §D2 [OWN-DATA] (S3 resource-by-id ownership) — the inner ownership gate inside
/// <c>CreateDispute.Handler</c>. The <c>CanCreateDispute → CustomerOnly</c> policy is the coarse
/// outer gate; this handler check is the inner gate that decides *which* customer's order may be
/// disputed, and holds regardless of host or invocation path.
///   - a customer disputing an order owned by a DIFFERENT user gets the not-found business error
///     (<see cref="BusinessErrorMessage.OrderNotFound"/>) — NotFound, not Forbidden — and NOTHING is
///     added to the dispute repository;
///   - a customer disputing an OrderId that does not exist gets the SAME OrderNotFound (no
///     enumeration difference between "missing" and "not yours");
///   - a customer disputing an order they OWN, with no open dispute, gets a created Dispute and
///     <c>Success(dispute.Id)</c> — the existing happy path is preserved;
///   - regression: the existing <see cref="BusinessErrorMessage.DisputeAlreadyExists"/> pre-check
///     still fires for an order that already has an open dispute, with nothing added.
/// </summary>
public class CreateDisputeHandlerTests
{
    private const string CallerUserId = "caller-user-1";
    private const string OtherUserId = "other-user-2";
    private const string OwnedOrderId = "order-owned-1";
    private const string OtherOrderId = "order-other-2";
    private const string MissingOrderId = "order-missing-3";

    private readonly Mock<IDisputeRepository> _disputeRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public CreateDisputeHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CallerUserId);
        // Default: no open dispute exists for any order (the happy path precondition).
        _disputeRepository
            .Setup(r => r.GetOpenDisputeForOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dispute?)null);
    }

    private CreateDispute.Handler CreateHandler() =>
        (CreateDispute.Handler)Activator.CreateInstance(
            typeof(CreateDispute.Handler),
            _disputeRepository.Object,
            _orderRepository.Object,
            _session.Object)!;

    /// <summary>
    /// The clean defaults to YESTERDAY. It used to default to tomorrow, which every happy-path test
    /// here relied on without noticing — a dispute could be filed against a booking that had not
    /// happened, and nothing said no.
    /// </summary>
    private Order ArrangeOrder(string orderId, string? ownerUserId, DateTime? cleaningDateTime = null)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime ?? DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "currency-1",
            paymentStatus: PaymentStatus.Pending,
            userId: ownerUserId);
        order.Id = orderId;

        _orderRepository
            .Setup(r => r.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        return order;
    }

    // ── the clean has to have happened ──────────────────────────────────────

    [Fact]
    public async Task ACleaningStillInTheFuture_CannotBeDisputed()
    {
        ArrangeOrder(OwnedOrderId, CallerUserId, cleaningDateTime: DateTime.UtcNow.AddDays(1));

        var result = await CreateHandler().Handle(ValidCommand(OwnedOrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.DisputeCleaningNotStarted, result.Error!.Message);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Never);
    }

    /// <summary>
    /// The gate is the SCHEDULED TIME, not the order status — and this is the case that forces it. A
    /// cleaner who never arrives leaves the order at Confirmed with no completion to point at, so a
    /// "must be Completed" rule would refuse the one thing the guarantee exists for.
    /// </summary>
    [Fact]
    public async Task ANoShow_IsStillDisputable_ThoughTheOrderNeverCompleted()
    {
        var order = ArrangeOrder(OwnedOrderId, CallerUserId, cleaningDateTime: DateTime.UtcNow.AddHours(-3));

        var result = await CreateHandler().Handle(
            new CreateDispute.Command(
                OwnedOrderId, DisputeReason.ServiceNotProvided, "Nobody came and nobody called."),
            CancellationToken.None);

        Assert.NotEqual(OrderStatus.Completed, order.CurrentStatus);
        Assert.True(result.IsSuccess, result.Error?.Message);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Once);
    }

    /// <summary>
    /// Late is not refused. The window gates the guarantee, not the door — owner ruling 2026-09-05 —
    /// so a serious claim arriving after 24 h is still recorded and judged on its merits.
    /// </summary>
    [Fact]
    public async Task ALateDispute_IsAcceptedRatherThanRefused()
    {
        ArrangeOrder(OwnedOrderId, CallerUserId, cleaningDateTime: DateTime.UtcNow.AddDays(-30));

        var result = await CreateHandler().Handle(ValidCommand(OwnedOrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Once);
    }

    // ── the items the customer names ────────────────────────────────────────

    private static Service Svc(string id, string name)
    {
        var s = Service.Create("cat-1", name, "", 100m, 0m);
        s.Id = id;
        return s;
    }

    [Fact]
    public async Task ADisputeCanNameTheItemsThatWereNotDoneProperly()
    {
        var order = ArrangeOrder(OwnedOrderId, CallerUserId);
        var svcOrder = Svc("svc-oven", "Oven cleaning");
        order.AddSelectedServices([OrderService.Create(
            order, svcOrder, svcOrder.BasePrice, svcOrder.PerRoomPrice,
            svcOrder.BasePrice + svcOrder.PerRoomPrice * (order.Rooms + order.Bathrooms))]);

        Dispute? saved = null;
        _disputeRepository.Setup(r => r.Add(It.IsAny<Dispute>())).Callback<Dispute>(d => saved = d);

        var result = await CreateHandler().Handle(
            ValidCommand(OwnedOrderId) with
            {
                Lines = [new CreateDispute.DisputeLineSelection("svc-oven", null)],
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var line = Assert.Single(saved!.Lines);
        Assert.Equal("svc-oven", line.ServiceId);
        Assert.Null(line.PackageId);
    }

    /// <summary>
    /// A line that is not on the order is refused — otherwise a customer could attach any service in
    /// the catalogue to their dispute and an admin resolving it would be reading a claim about work
    /// that was never bought.
    /// </summary>
    [Fact]
    public async Task AnItemThatIsNotOnTheOrder_IsRefused()
    {
        var order = ArrangeOrder(OwnedOrderId, CallerUserId);
        var svcOrder = Svc("svc-oven", "Oven cleaning");
        order.AddSelectedServices([OrderService.Create(
            order, svcOrder, svcOrder.BasePrice, svcOrder.PerRoomPrice,
            svcOrder.BasePrice + svcOrder.PerRoomPrice * (order.Rooms + order.Bathrooms))]);

        var result = await CreateHandler().Handle(
            ValidCommand(OwnedOrderId) with
            {
                Lines = [new CreateDispute.DisputeLineSelection("svc-never-bought", null)],
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.DisputeLineNotOnOrder, result.Error!.Message);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Never);
    }

    /// <summary>
    /// THE ENUMERATION ORACLE, denied. Someone else's order with a bogus line must answer exactly the
    /// same thing as someone else's order with a valid one — OrderNotFound, never "that line is not on
    /// it". Checking line membership before the ownership gate would have made this endpoint tell an
    /// attacker what another customer bought.
    /// </summary>
    [Fact]
    public async Task ALineOnSomebodyElsesOrder_IsIndistinguishableFromAMissingOrder()
    {
        var other = ArrangeOrder(OtherOrderId, OtherUserId);
        var svcOther = Svc("svc-oven", "Oven cleaning");
        other.AddSelectedServices([OrderService.Create(
            other, svcOther, svcOther.BasePrice, svcOther.PerRoomPrice,
            svcOther.BasePrice + svcOther.PerRoomPrice * (other.Rooms + other.Bathrooms))]);

        var withRealLine = await CreateHandler().Handle(
            ValidCommand(OtherOrderId) with
            {
                Lines = [new CreateDispute.DisputeLineSelection("svc-oven", null)],
            },
            CancellationToken.None);

        var withBogusLine = await CreateHandler().Handle(
            ValidCommand(OtherOrderId) with
            {
                Lines = [new CreateDispute.DisputeLineSelection("svc-nonsense", null)],
            },
            CancellationToken.None);

        Assert.Equal(BusinessErrorMessage.OrderNotFound, withRealLine.Error!.Message);
        Assert.Equal(withRealLine.Error!.Message, withBogusLine.Error!.Message);
    }

    [Fact]
    public async Task NamingNoItemsIsOrdinary()
    {
        ArrangeOrder(OwnedOrderId, CallerUserId);

        var result = await CreateHandler().Handle(ValidCommand(OwnedOrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    private static CreateDispute.Command ValidCommand(string orderId) =>
        new(orderId, DisputeReason.QualityIssue, "The cleaning was not done as agreed at all.");

    [Fact]
    public async Task Customer_Disputing_Order_Owned_By_Another_User_Returns_NotFound_And_Adds_Nothing()
    {
        ArrangeOrder(OtherOrderId, OtherUserId);
        var handler = CreateHandler();

        var result = await handler.Handle(ValidCommand(OtherOrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Never);
    }

    [Fact]
    public async Task Customer_Disputing_NonExistent_Order_Returns_NotFound_And_Adds_Nothing()
    {
        // No GetByIdAsync setup for this id → repo returns null (order does not exist).
        var handler = CreateHandler();

        var result = await handler.Handle(ValidCommand(MissingOrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Never);
    }

    [Fact]
    public async Task Customer_Disputing_Own_Order_With_No_Open_Dispute_Creates_Dispute_And_Returns_Id()
    {
        ArrangeOrder(OwnedOrderId, CallerUserId);
        Dispute? added = null;
        _disputeRepository.Setup(r => r.Add(It.IsAny<Dispute>()))
            .Callback<Dispute>(d => added = d);
        var handler = CreateHandler();

        var result = await handler.Handle(ValidCommand(OwnedOrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Once);
        Assert.NotNull(added);
        Assert.Equal(added!.Id, result.Value!.DisputeId);
        Assert.Equal(OwnedOrderId, added.OrderId);
        Assert.Equal(CallerUserId, added.UserId);
    }

    [Fact]
    public async Task Customer_Disputing_Own_Order_With_Existing_Open_Dispute_Returns_AlreadyExists_And_Adds_Nothing()
    {
        ArrangeOrder(OwnedOrderId, CallerUserId);
        var openDispute = new Dispute(
            orderId: OwnedOrderId,
            userId: CallerUserId,
            reason: DisputeReason.QualityIssue,
            description: "Already opened a dispute earlier.",
            createdBy: CallerUserId);
        _disputeRepository
            .Setup(r => r.GetOpenDisputeForOrderAsync(OwnedOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(openDispute);
        var handler = CreateHandler();

        var result = await handler.Handle(ValidCommand(OwnedOrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.DisputeAlreadyExists, result.Error!.Message);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Never);
    }
}
