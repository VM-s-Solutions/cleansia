using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Legal;
using Cleansia.Tests.Domain.Legal;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Cleansia.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Cleansia.Tests.Infrastructure;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0062 D3/D4 at the producer: a booking emits ONE <see cref="CreateOrder.OrderBookingEvidence"/>
/// read off the persisted order and the calculator's answer — never off the request, except the terms
/// tick, which only the client can assert. A guest row says so and carries the standard cancellation
/// window; a Plus member's carries the plan's. No name, contact, address text, instructions, quoted
/// price or preferred cleaner ever lands on it, and a refused booking emits nothing.
/// </summary>
public sealed class CreateOrderAuditEvidenceTests
{
    private const string UserId = "user-ev-1";
    private const string CreatedOrderId = "order-ev-created";

    private readonly Mock<IAddressRepository> _addressRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<IServiceCityRepository> _serviceCityRepository = new();
    private readonly Mock<IStripeClientFactory> _stripeClientFactory = new();
    private readonly Mock<IStripeClient> _stripeClient = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = ExpressWaiverMocks.NoConsumer();
    private readonly Mock<ICreditAccountRepository> _creditAccountRepository = new();
    private readonly Mock<IPromoCodeService> _promoCodeService = new();
    private readonly Mock<IReferralService> _referralService = new();
    private readonly Mock<IReferralRepository> _referralRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IOrderFactory> _orderFactory = new();
    private readonly Mock<IAddressGeocoder> _addressGeocoder = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly LegalDocument _terms = LegalDocumentFixtures.Terms();
    private readonly Mock<ILegalDocumentResolver> _legalDocuments;
    private readonly AuditContext _auditContext = new();

    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();

    public CreateOrderAuditEvidenceTests()
    {
        _legalDocuments = LegalDocumentFixtures.Resolver(_terms, LegalDocumentFixtures.Privacy());
        _countryRepository
            .Setup(r => r.IsServicedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _serviceCityRepository
            .Setup(r => r.CityIsServicedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());
        _stripeClientFactory.Setup(f => f.CreateClient()).Returns(_stripeClient.Object);
        _stripeClient
            .Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<Cleansia.Core.Domain.Orders.Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutSessionResult("cs_test_ev", "https://checkout.stripe.com/c/pay/cs_test_ev"));
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
                    CleaningDateTime = input.CleaningDate,
                    Rooms = input.Rooms,
                    Bathrooms = input.Bathrooms,
                    TenantId = "tenant-1",
                }, currency: Czk));
        _membershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
    }

    private CreateOrder.Handler CreateHandler() =>
        new(
            OrderMarketDoubles.Trading(Czk),
            _session.Object,
            _pricingCalculator.Object,
            _orderFactory.Object,
            new OrderAddressResolver(
                _addressRepository.Object, _savedAddressRepository.Object, _countryRepository.Object,
                _serviceCityRepository.Object, _addressGeocoder.Object),
            new OrderPromoApplier(_promoCodeService.Object, NullLogger<OrderPromoApplier>.Instance),
            new OrderLateReferralAcceptor(_referralService.Object, _referralRepository.Object, NullLogger<OrderLateReferralAcceptor>.Instance),
            new OrderPaymentDispatcher(
                _stripeClientFactory.Object, _pending.Object, new OrderChannelProvider(OrderChannel.Web),
                new StripeConfig(new ConfigurationBuilder().Build()), NullLogger<OrderPaymentDispatcher>.Instance),
            TestGuestOrderAccessTokenIssuer.WithNoLiveTokens(),
            _expressWaiverConsumer.Object,
            _creditAccountRepository.Object,
            new CancellationPolicyResolver(_membershipRepository.Object),
            _legalDocuments.Object,
            OrderMarketDoubles.OperatedBy("tenant-1"),
            Mock.Of<ITenantProvider>(),
            _auditContext,
            NullLogger<CreateOrder.Handler>.Instance);

    private static JsonElement Payload(AuditSnapshot? snapshot) => JsonDocument.Parse(snapshot!.AfterJson!).RootElement;

    [Fact]
    public void The_Marker_Is_Frozen_On_The_Order_And_Admits_A_Guest()
    {
        var descriptor = AuditActionDescriptor.For(typeof(CreateOrder.Command));

        Assert.Equal("customer.order.create", descriptor.Action);
        Assert.Equal("Order", descriptor.ResourceType);
        Assert.Equal(AuditAudience.Customer, descriptor.Audience);
        Assert.True(descriptor.AllowsAnonymousActor);
    }

    // The gate is the validator's; the wire member stays optional so a client that omits it binds
    // null and is refused as such, rather than failing to bind at all.
    [Fact]
    public void The_Terms_Tick_Stays_Nullable_On_The_Wire_And_An_Absent_Member_Lands_Null()
    {
        var parameter = typeof(CreateOrder.Command).GetConstructors().Single().GetParameters()
            .Single(p => p.Name == nameof(CreateOrder.Command.TermsAccepted));

        Assert.Equal(typeof(bool?), parameter.ParameterType);
        Assert.True(parameter.HasDefaultValue);
        Assert.Null(parameter.DefaultValue);
    }

    [Fact]
    public async Task A_Guest_Booking_Records_The_Server_Figures_The_Standard_Window_And_The_Tick_As_Sent()
    {
        _session.Setup(s => s.GetUserId()).Returns((string?)null);
        var command = CreateOrderTestData.ValidCommand(paymentType: PaymentType.Cash) with { TermsAccepted = true };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("Order", snapshot!.ResourceType);
        Assert.Equal(CreatedOrderId, snapshot.ResourceId);
        Assert.Null(snapshot.ActorUserId);

        var payload = Payload(snapshot);
        Assert.Equal(CreatedOrderId, payload.GetProperty("orderId").GetString());
        Assert.True(payload.GetProperty("isGuest").GetBoolean());
        Assert.True(payload.GetProperty("termsAccepted").GetBoolean());
        Assert.Equal(_terms.Version, payload.GetProperty("termsVersionAccepted").GetString());
        _legalDocuments.Verify(
            r => r.ResolveInForceAsync(LegalDocumentType.TermsOfService, command.CustomerAddress!.CountryId, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(CreateOrderTestData.MatchingTotalPrice, payload.GetProperty("totalPrice").GetDecimal());
        Assert.Equal("CZK", payload.GetProperty("currencyCode").GetString());
        Assert.Equal("cz", payload.GetProperty("countryId").GetString());
        Assert.Equal("cash", payload.GetProperty("paymentType").GetString());
        Assert.Equal(0m, payload.GetProperty("expressSurchargeAmount").GetDecimal());
        Assert.False(payload.GetProperty("expressWaivedByMembership").GetBoolean());
        Assert.Equal(0m, payload.GetProperty("creditAppliedAmount").GetDecimal());
        Assert.InRange(payload.GetProperty("leadTimeHours").GetDecimal(), 71.9m, 72.0m);
        Assert.Equal([CreateOrderTestData.PackageId], payload.GetProperty("packageIds").EnumerateArray().Select(e => e.GetString()).ToList());
        Assert.Equal([CreateOrderTestData.ServiceId], payload.GetProperty("serviceIds").EnumerateArray().Select(e => e.GetString()).ToList());
        Assert.Empty(payload.GetProperty("extraSlugs").EnumerateArray());
        Assert.Equal(2, payload.GetProperty("rooms").GetInt32());
        Assert.Equal(1, payload.GetProperty("bathrooms").GetInt32());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("savedAddressId").ValueKind);
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("addressId").GetString()));
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("recurringTemplateId").ValueKind);
        Assert.Equal("en", payload.GetProperty("language").GetString());

        var shown = payload.GetProperty("cancellationPolicyShown");
        Assert.Equal(BookingPolicy.FreeCancellationHours, shown.GetProperty("freeHours").GetInt32());
        Assert.Equal(BookingPolicy.PartialCancellationHours, shown.GetProperty("partialHours").GetInt32());
        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, shown.GetProperty("partialRate").GetDecimal());
        Assert.Equal(BookingPolicy.LastMinuteCancellationFeeRate, shown.GetProperty("lastMinuteRate").GetDecimal());
        Assert.Equal(BookingPolicy.FreeCancellationHours, shown.GetProperty("freeHoursForThisCustomer").GetInt32());

        var members = payload.EnumerateObject().Select(p => p.Name).ToList();
        Assert.DoesNotContain("quotedTotalPrice", members);
        Assert.DoesNotContain("preferredEmployeeId", members);
        Assert.DoesNotContain(members, m => m.Contains("name", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, m => m.Contains("email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, m => m.Contains("phone", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, m => m.Contains("instructions", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(command.CustomerEmail, snapshot.AfterJson!);
        Assert.DoesNotContain(command.CustomerAddress!.Street, snapshot.AfterJson!);
    }

    // A consented customer sends nothing (the validator lets that through); the handler records the
    // absence as null rather than inventing a tick.
    [Fact]
    public async Task A_Booking_That_Sends_No_Tick_Is_Recorded_As_Not_Asserted()
    {
        _session.Setup(s => s.GetUserId()).Returns((string?)null);

        var result = await CreateHandler().Handle(CreateOrderTestData.ValidCommand(termsAccepted: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(JsonValueKind.Null, Payload(_auditContext.DrainSnapshot()).GetProperty("termsAccepted").ValueKind);
    }

    [Fact]
    public async Task A_Plus_Members_Booking_Carries_The_Plans_Free_Window_And_Is_Not_A_Guest()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        var plan = MembershipPlan.Create("PLUS_MONTHLY", "Plus", 5m, freeCancellationWindowHours: 4, allowsExpressUpgrade: true);
        var membership = UserMembershipMockFactory.Paid(UserId, plan.Id);
        typeof(UserMembership).GetProperty(nameof(UserMembership.MembershipPlan))!
            .GetSetMethod(nonPublic: true)!.Invoke(membership, [plan]);
        _membershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var result = await CreateHandler().Handle(
            CreateOrderTestData.ValidCommand(paymentType: PaymentType.Card, preferredEmployeeId: "emp-favourite"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var snapshot = _auditContext.DrainSnapshot();
        var payload = Payload(snapshot);
        Assert.False(payload.GetProperty("isGuest").GetBoolean());
        Assert.Equal(4, payload.GetProperty("cancellationPolicyShown").GetProperty("freeHoursForThisCustomer").GetInt32());
        Assert.Equal("card", payload.GetProperty("paymentType").GetString());
        Assert.DoesNotContain("emp-favourite", snapshot!.AfterJson!);
    }

    [Fact]
    public async Task The_Extras_Recorded_Are_The_Orders_Resolved_Slugs_Never_The_Requests_Keys()
    {
        const string smuggled = "not-a-slug my phone 777";
        _session.Setup(s => s.GetUserId()).Returns((string?)null);
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateOrderInput input, CancellationToken _) =>
            {
                var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
                {
                    Id = CreatedOrderId,
                    UserId = input.UserId,
                    PaymentType = input.PaymentType,
                    TotalPrice = input.RawSubtotal,
                    CustomerAddress = input.Address,
                    CleaningDateTime = input.CleaningDate,
                    TenantId = "tenant-1",
                }, currency: Czk);
                var windows = Extra.Create("windows", "Windows", null);
                order.AddSelectedExtras([OrderExtra.Create(order, windows, unitPrice: 150m)]);
                return order;
            });
        var command = CreateOrderTestData.ValidCommand() with
        {
            Extras = new Dictionary<string, bool> { [smuggled] = true, ["windows"] = true },
        };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.Equal(["windows"], Payload(snapshot).GetProperty("extraSlugs").EnumerateArray().Select(e => e.GetString()).ToList());
        Assert.DoesNotContain(smuggled, snapshot!.AfterJson!);
    }

    [Fact]
    public async Task A_Refused_Booking_Records_No_Evidence()
    {
        _session.Setup(s => s.GetUserId()).Returns((string?)null);
        _serviceCityRepository
            .Setup(r => r.CityIsServicedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateHandler().Handle(CreateOrderTestData.ValidCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(_auditContext.DrainSnapshot());
    }
}
