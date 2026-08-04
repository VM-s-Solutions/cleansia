using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Tests.Common;
using Cleansia.Tests.Features.Orders;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// <c>TC-BENEFIT-CONSENT-0</c> (ADR-0035 AM-8) — the invariant Mode A's ordering makes possible to break:
///
/// <para><b>No path may persist an <c>Order.TotalPrice</c> greater than the <c>command.TotalPrice</c> the
/// validator approved.</b> When the slot is lost between a WAIVED validation and the reservation, the
/// naive handler prices at waived + 20%, freezes that into the order and charges it — with no error, no
/// confirmation and no field in the three-field response to notice it by.</para>
///
/// <para>Also pins the Mode A ordering itself: the slot is claimed BEFORE the factory is asked to build
/// anything, so no order ever carries a waived price without a committed slot.</para>
/// </summary>
public class CreateOrderExpressWaiverConsentTests
{
    private const string UserId = "user-consent-1";
    private const string CreatedOrderId = "order-consent-1";
    private const string PeriodKey = "C:2026-08";

    private readonly Mock<IAddressRepository> _addressRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<IServiceCityRepository> _serviceCityRepository = new();
    private readonly Mock<IStripeClientFactory> _stripeClientFactory = new();
    private readonly Mock<IStripeClient> _stripeClient = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<IPromoCodeService> _promoCodeService = new();
    private readonly Mock<IReferralService> _referralService = new();
    private readonly Mock<IReferralRepository> _referralRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IOrderFactory> _orderFactory = new();
    private readonly Mock<IAddressGeocoder> _addressGeocoder = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = new();

    public CreateOrderExpressWaiverConsentTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);

        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        _currencyRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        _currencyRepository
            .Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        _countryRepository
            .Setup(r => r.IsServicedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _serviceCityRepository
            .Setup(r => r.CityIsServicedAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());

        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateOrderInput input, CancellationToken _) =>
                OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
                {
                    Id = CreatedOrderId,
                    UserId = input.UserId,
                    PaymentType = input.PaymentType,
                    TotalPrice = input.RawSubtotal,
                    CustomerAddress = input.Address,
                }));
    }

    private CreateOrder.Handler CreateHandler() =>
        new(
            _currencyRepository.Object,
            _session.Object,
            _pricingCalculator.Object,
            _orderFactory.Object,
            new OrderAddressResolver(
                _addressRepository.Object,
                _savedAddressRepository.Object,
                _countryRepository.Object,
                _serviceCityRepository.Object,
                _addressGeocoder.Object),
            new OrderPromoApplier(
                _promoCodeService.Object,
                NullLogger<OrderPromoApplier>.Instance),
            new OrderLateReferralAcceptor(
                _referralService.Object,
                _referralRepository.Object,
                NullLogger<OrderLateReferralAcceptor>.Instance),
            new OrderPaymentDispatcher(
                _stripeClientFactory.Object,
                _pending.Object,
                new OrderChannelProvider(OrderChannel.Mobile),
                NullLogger<OrderPaymentDispatcher>.Instance),
            _expressWaiverConsumer.Object);

    /// <summary>
    /// The calculator's answer is what SET the price, so it is what the reservation must agree with.
    /// Separating the two lets the tests drive the window where a concurrent booking or release moves the
    /// quota between the price computation and the claim.
    /// </summary>
    private void ArrangeCalculatorWaived(bool waived)
        => _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing() with
            {
                ExpressSurchargeApplied = !waived,
                ExpressSurchargeAmount = waived ? 0m : 250m,
                ExpressSurchargeWaivedByMembership = waived,
            });

    private void ArrangeWaiver(bool waived, MembershipBenefitUsage? reservation)
    {
        ArrangeCalculatorWaived(waived);

        _expressWaiverConsumer
            .Setup(c => c.ResolveAsync(
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpressWaiver(
                InExpressWindow: true,
                Waived: waived,
                Quota: 2,
                RemainingBeforeThisBooking: waived ? 1 : 0,
                PeriodKey: PeriodKey,
                UserId: UserId,
                UserMembershipId: "membership-1"));
        _expressWaiverConsumer
            .Setup(c => c.TryReserveAsync(
                It.IsAny<ExpressWaiver>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
    }

    private static MembershipBenefitUsage Reservation() => MembershipBenefitUsage.Create(
        UserId, MembershipBenefitKind.ExpressUpgrade, PeriodKey, 0, "membership-1", DateTime.UtcNow);

    [Fact]
    public async Task ReservationLostAfterAWaivedValidation_FailsInsteadOfUpcharging()
    {
        ArrangeWaiver(waived: true, reservation: null);

        var result = await CreateHandler().Handle(
            CreateOrderTestData.ValidCommand(paymentType: PaymentType.Card), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.ExpressWaiverNoLongerAvailable, result.Error!.Message);

        // No order, and no Stripe session — the customer re-quotes rather than being charged 20% more
        // than the price they submitted and the server approved.
        _orderFactory.Verify(
            f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _stripeClient.Verify(
            c => c.CreateCheckoutSessionAsync(
                It.IsAny<Cleansia.Core.Domain.Orders.Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReservationHeld_PassesTheReservedWaiverIntoTheFactory()
    {
        var reservation = Reservation();
        ArrangeWaiver(waived: true, reservation: reservation);

        CreateOrderInput? captured = null;
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .Callback((CreateOrderInput input, CancellationToken _) => captured = input)
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = CreatedOrderId,
                UserId = UserId,
                PaymentType = PaymentType.Cash,
            }));

        var result = await CreateHandler().Handle(
            CreateOrderTestData.ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Same(reservation, captured!.ReservedExpressWaiver);
        _expressWaiverConsumer.Verify(
            c => c.AttachOrderAsync(reservation, CreatedOrderId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// A quota already exhausted at quote time is a PRICE, not a failure: the quote carries the
    /// surcharge, the customer consents to the charged total and nothing is blocked.
    /// </summary>
    [Fact]
    public async Task QuotaAlreadyExhaustedAtQuoteTime_DoesNotBlockTheBooking()
    {
        ArrangeWaiver(waived: false, reservation: null);

        var result = await CreateHandler().Handle(
            CreateOrderTestData.ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    /// <summary>
    /// Mode A: the claim happens BEFORE the factory builds anything. Reversing the two would make the cap
    /// soft, which is farmable by every subscriber with concurrent requests alone — an express waiver
    /// requires nothing but a live subscription, unlike a promo code an operator had to issue.
    /// </summary>
    [Fact]
    public async Task TheSlotIsClaimedBeforeTheOrderIsBuilt()
    {
        ArrangeWaiver(waived: true, reservation: Reservation());

        var sequence = new List<string>();
        _expressWaiverConsumer
            .Setup(c => c.TryReserveAsync(
                It.IsAny<ExpressWaiver>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("reserve"))
            .ReturnsAsync(Reservation());
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("create"))
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = CreatedOrderId,
                UserId = UserId,
                PaymentType = PaymentType.Cash,
            }));

        await CreateHandler().Handle(CreateOrderTestData.ValidCommand(), CancellationToken.None);

        Assert.Equal(["reserve", "create"], sequence);
    }

    /// <summary>
    /// The race in the OTHER direction: the calculator charged the surcharge (quota was full when the
    /// price was computed) and a slot came free a moment later. Nothing may be claimed — a live slot on
    /// an order whose frozen price carries the surcharge is the one loss with no release rule and no
    /// orphan sweep, because the OrderId is stamped.
    /// </summary>
    [Fact]
    public async Task ASlotFreedAfterTheChargedPriceWasComputed_IsNotClaimed()
    {
        ArrangeCalculatorWaived(waived: false);
        _expressWaiverConsumer
            .Setup(c => c.ResolveAsync(
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpressWaiver(true, Waived: true, 2, 1, PeriodKey, UserId, "membership-1"));
        _expressWaiverConsumer
            .Setup(c => c.TryReserveAsync(
                It.IsAny<ExpressWaiver>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Reservation());

        CreateOrderInput? captured = null;
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .Callback((CreateOrderInput input, CancellationToken _) => captured = input)
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = CreatedOrderId,
                UserId = UserId,
                PaymentType = PaymentType.Cash,
            }));

        var result = await CreateHandler().Handle(
            CreateOrderTestData.ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(captured!.ReservedExpressWaiver);
        _expressWaiverConsumer.Verify(
            c => c.TryReserveAsync(
                It.IsAny<ExpressWaiver>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// TC-BENEFIT-CLOCK-0 — one captured <c>nowUtc</c> across the resolve, the reservation and the
    /// factory. Two clock reads inside a request can put the resolver and the policy on opposite sides of
    /// the 4h boundary, producing a live slot on an order whose price carries no surcharge: a state no
    /// release rule covers and the orphan sweep never sees, because the OrderId IS stamped.
    /// </summary>
    [Fact]
    public async Task OneClockReadingIsThreadedThroughTheWholeExpressPath()
    {
        ArrangeWaiver(waived: true, reservation: Reservation());

        DateTime resolveNow = default;
        DateTime reserveNow = default;
        DateTime factoryNow = default;

        _expressWaiverConsumer
            .Setup(c => c.ResolveAsync(
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback((string? _, DateTime? __, DateTime now, CancellationToken ___) => resolveNow = now)
            .ReturnsAsync(new ExpressWaiver(true, true, 2, 1, PeriodKey, UserId, "membership-1"));
        _expressWaiverConsumer
            .Setup(c => c.TryReserveAsync(
                It.IsAny<ExpressWaiver>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback((ExpressWaiver _, DateTime now, CancellationToken __) => reserveNow = now)
            .ReturnsAsync(Reservation());
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .Callback((CreateOrderInput input, CancellationToken _) => factoryNow = input.NowUtc)
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = CreatedOrderId,
                UserId = UserId,
                PaymentType = PaymentType.Cash,
            }));

        await CreateHandler().Handle(CreateOrderTestData.ValidCommand(), CancellationToken.None);

        Assert.Equal(resolveNow, reserveNow);
        Assert.Equal(resolveNow, factoryNow);
    }
}
