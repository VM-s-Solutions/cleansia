using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Tests.Common;
using Cleansia.Tests.Domain.Legal;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-10-03: an occurrence is created up to a week ahead under whatever terms are in force
/// then, so confirming it asks for the terms tick on exactly a booking's rule — required while either legal
/// consent is not an acceptance of the text in force for the occurrence's market, refused on
/// <see cref="BusinessErrorMessage.TermsNotAccepted"/> without it. With it, both consent rows move to the
/// texts in force under the account's company, and the confirmation's evidence names the versions.
/// </summary>
public sealed class ConfirmRecurringOrderTermsTickTests
{
    private const string OrderId = "order-recurring-terms";
    private const string CustomerUserId = "user-recurring-terms";
    private const string Czechia = "country-cz";
    private const string AccountCompany = "company-of-the-account";
    private const string OrderCompany = "company-of-the-order";

    private static readonly DateOnly September14 = new(2026, 9, 14);
    private static readonly DateOnly October3 = new(2026, 10, 3);

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IUserConsentRepository> _consents = new();
    private readonly Mock<ILegalDocumentResolver> _resolver = new();
    private readonly Mock<IConsentService> _consentService = new();
    private readonly Mock<ITenantProvider> _tenants = new();
    private readonly AuditContext _auditContext = new();
    private readonly List<string?> _companiesGrantedUnder = [];
    private string? _ambientCompany = AccountCompany;

    private readonly LegalDocument _oldTerms = LegalDocumentFixtures.Terms(September14);
    private readonly LegalDocument _newTerms = LegalDocumentFixtures.Terms(October3);
    private readonly LegalDocument _privacy = LegalDocumentFixtures.Privacy(September14);

    public ConfirmRecurringOrderTermsTickTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CustomerUserId);
        _resolver
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.TermsOfService, Czechia, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_newTerms);
        _resolver
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.PrivacyPolicy, Czechia, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_privacy);
        _tenants.Setup(t => t.GetCurrentTenantId()).Returns(() => _ambientCompany);
        _tenants.Setup(t => t.SetTenantOverride(It.IsAny<string>())).Callback<string>(id => _ambientCompany = id);
        _consentService
            .Setup(s => s.TryGrantAsync(CustomerUserId, It.IsAny<ConsentType>(), It.IsAny<LegalDocument?>(), It.IsAny<CancellationToken>()))
            .Callback(() => _companiesGrantedUnder.Add(_ambientCompany))
            .ReturnsAsync(true);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task A_Customer_Who_Accepted_Older_Terms_Is_Refused_Without_The_Tick(bool? termsAccepted)
    {
        ArrangeOccurrence();
        OnRecord(Accepted(ConsentType.TermsOfService, _oldTerms), Accepted(ConsentType.PrivacyPolicy, _privacy));

        var result = await Validator().TestValidateAsync(new ConfirmRecurringOrder.Command(OrderId, TermsAccepted: termsAccepted));

        result.ShouldHaveValidationErrorFor(x => x.TermsAccepted)
            .WithErrorMessage(BusinessErrorMessage.TermsNotAccepted)
            .WithErrorCode(nameof(ConfirmRecurringOrder.Command.TermsAccepted));
    }

    [Fact]
    public async Task A_Customer_Who_Accepted_Older_Terms_Confirms_With_The_Tick()
    {
        ArrangeOccurrence();
        OnRecord(Accepted(ConsentType.TermsOfService, _oldTerms), Accepted(ConsentType.PrivacyPolicy, _privacy));

        var result = await Validator().TestValidateAsync(new ConfirmRecurringOrder.Command(OrderId, TermsAccepted: true));

        result.ShouldNotHaveValidationErrorFor(x => x.TermsAccepted);
    }

    [Fact]
    public async Task A_Customer_Holding_The_Texts_In_Force_Needs_No_Tick()
    {
        ArrangeOccurrence();
        OnRecord(Accepted(ConsentType.TermsOfService, _newTerms), Accepted(ConsentType.PrivacyPolicy, _privacy));

        var result = await Validator().TestValidateAsync(new ConfirmRecurringOrder.Command(OrderId));

        result.ShouldNotHaveValidationErrorFor(x => x.TermsAccepted);
    }

    [Fact]
    public async Task Another_Customers_Occurrence_Is_Left_To_The_Handlers_Not_Found()
    {
        ArrangeOccurrence(ownerUserId: "someone-else");
        OnRecord();

        var result = await Validator().TestValidateAsync(new ConfirmRecurringOrder.Command(OrderId));

        result.ShouldNotHaveValidationErrorFor(x => x.TermsAccepted);
    }

    [Fact]
    public async Task An_Occurrence_No_Longer_Awaiting_Confirmation_Is_Left_To_The_Handlers_Answer()
    {
        ArrangeOccurrence(paymentStatus: PaymentStatus.Paid);
        OnRecord();

        var result = await Validator().TestValidateAsync(new ConfirmRecurringOrder.Command(OrderId));

        result.ShouldNotHaveValidationErrorFor(x => x.TermsAccepted);
    }

    [Fact]
    public async Task The_Tick_Moves_Both_Consents_To_The_Texts_In_Force_Under_The_Accounts_Company()
    {
        ArrangeOccurrence();

        var result = await Handler().Handle(
            new ConfirmRecurringOrder.Command(OrderId, TermsAccepted: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        _consentService.Verify(s => s.TryGrantAsync(CustomerUserId, ConsentType.TermsOfService, _newTerms, It.IsAny<CancellationToken>()), Times.Once);
        _consentService.Verify(s => s.TryGrantAsync(CustomerUserId, ConsentType.PrivacyPolicy, _privacy, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(new string?[] { AccountCompany, AccountCompany }, _companiesGrantedUnder);
        var payload = Evidence();
        Assert.True(payload.GetProperty("termsAccepted").GetBoolean());
        Assert.Equal("2026-10-03", payload.GetProperty("termsVersionAccepted").GetString());
        Assert.Equal("2026-09-14", payload.GetProperty("privacyVersionAccepted").GetString());
    }

    [Fact]
    public async Task Without_The_Tick_Nothing_Is_Granted_And_The_Evidence_Names_The_Versions_Held()
    {
        ArrangeOccurrence();
        OnRecord(Accepted(ConsentType.TermsOfService, _newTerms), Accepted(ConsentType.PrivacyPolicy, _privacy));

        var result = await Handler().Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        _consentService.VerifyNoOtherCalls();
        var payload = Evidence();
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("termsAccepted").ValueKind);
        Assert.Equal("2026-10-03", payload.GetProperty("termsVersionAccepted").GetString());
        Assert.Equal("2026-09-14", payload.GetProperty("privacyVersionAccepted").GetString());
    }

    [Fact]
    public async Task A_Refused_Confirmation_Moves_No_Consent()
    {
        ArrangeOccurrence(paymentStatus: PaymentStatus.Paid);

        var result = await Handler().Handle(
            new ConfirmRecurringOrder.Command(OrderId, TermsAccepted: true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderPaymentAlreadyPaid, result.Error!.Message);
        _consentService.VerifyNoOtherCalls();
    }

    private JsonElement Evidence() =>
        JsonDocument.Parse(_auditContext.DrainSnapshot()!.AfterJson!).RootElement;

    private void OnRecord(params UserConsent[] consents) =>
        _consents
            .Setup(r => r.GetByUserIdNoTrackingAsync(CustomerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consents.ToList());

    private static UserConsent Accepted(ConsentType type, LegalDocument document) =>
        UserConsent.Grant(CustomerUserId, type, "203.0.113.9", "Chrome", document.Version, document.Id);

    private ConfirmRecurringOrder.Validator Validator() =>
        new(_session.Object, OrderAccessDoubles.Over(_orderRepository, _session), _consents.Object, _resolver.Object);

    private ConfirmRecurringOrder.Handler Handler() =>
        new(
            OrderAccessDoubles.Over(_orderRepository, _session),
            _orderRepository.Object,
            Mock.Of<ISavedCardRepository>(), Mock.Of<IReceivableRepository>(),
            Mock.Of<ICreditAccountRepository>(),
            Mock.Of<IUserRepository>(),
            _session.Object,
            _tenants.Object,
            Mock.Of<IStripeClient>(),
            new StripeConfig(new ConfigurationBuilder().Build()),
            Mock.Of<IStripeCustomerResolver>(),
            Mock.Of<IRequestMetadataProvider>(),
            new OrderChannelProvider(OrderChannel.Mobile),
            Mock.Of<IPendingDispatch>(),
            Mock.Of<INotificationProducer>(),
            NoPreferredCleanerHold.Resolver,
            Mock.Of<IAdminNotifier>(),
            _consentService.Object,
            _consents.Object,
            _resolver.Object,
            _auditContext,
            NullLogger<ConfirmRecurringOrder.Handler>.Instance);

    private void ArrangeOccurrence(
        string ownerUserId = CustomerUserId, PaymentStatus paymentStatus = PaymentStatus.Pending)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "jana@example.com",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Dlouhá 12", "Praha", "11000", Czechia),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(48),
            paymentType: PaymentType.Cash,
            totalPrice: 900m,
            currencyId: currency.Id,
            paymentStatus: paymentStatus,
            userId: ownerUserId,
            recurringTemplateId: "tmpl-weekly",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.TenantId = OrderCompany;
        order.SetCurrency(currency);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        _orderRepository.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
    }
}
