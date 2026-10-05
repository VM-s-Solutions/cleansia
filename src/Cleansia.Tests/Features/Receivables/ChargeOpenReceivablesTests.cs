using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Receivables;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StripeError = Stripe.StripeError;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Features.Receivables;

/// <summary>
/// The off-session charge sweep (owner ruling 2026-09-28, decisions 16 to 18). Switched off, it reads and
/// charges nothing — the state it stays in, since the owner ruled on 2026-10-04 that no saved card is charged.
/// Switched on, it charges each open receivable once to the customer's usable card in its currency saved under
/// a card-guarantee consent, the attempt committed before the call; a card saved under any other consent is
/// never charged; a pay link the customer holds is closed first, and one they have already paid is not
/// charged again; a customer with no usable card is not charged, and a declined charge leaves the
/// receivable open for the failure webhook's pay link. With card payments switched off it refuses.
/// </summary>
public class ChargeOpenReceivablesTests
{
    private const string TenantId = "tenant-sweep";
    private const string CardGuaranteeConsent = "card-guarantee-draft-2026-09-28";

    private readonly Mock<IPaymentsConfig> _payments = new();
    private readonly Mock<IStripeConfig> _stripeConfig = new();
    private readonly Mock<IReceivableRepository> _receivables = new();
    private readonly Mock<ISavedCardRepository> _savedCards = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Mock<IStripeClientFactory> _stripeFactory = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly List<string> _sequence = [];
    private readonly Receivable _receivable = OpenReceivable();

    public ChargeOpenReceivablesTests()
    {
        _payments.SetupGet(p => p.OffSessionChargesEnabled).Returns(true);
        _stripeConfig.SetupGet(c => c.Enabled).Returns(true);
        _stripeFactory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        _receivables
            .Setup(r => r.GetUnchargedOpenIgnoringTenantAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([_receivable]);
        _unitOfWork
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _sequence.Add($"commit attempt {_receivable.Attempts}"))
            .Returns(Task.CompletedTask);
        _stripe
            .Setup(c => c.ChargeReceivableOffSessionAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback(() => _sequence.Add("charge"))
            .ReturnsAsync("pi_off_session");
    }

    private ChargeOpenReceivables.Handler Handler() => new(
        _payments.Object,
        _stripeConfig.Object,
        _receivables.Object,
        _savedCards.Object,
        _stripeFactory.Object,
        _tenantProvider.Object,
        _unitOfWork.Object,
        TimeProvider.System,
        NullLogger<ChargeOpenReceivables.Handler>.Instance);

    private static Receivable OpenReceivable()
    {
        var order = Order.Create(
            customerName: "Owing Customer",
            customerEmail: "owing@example.com",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: "currency-czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-owing",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        var receivable = Receivable.ForCashCancellationFee(order, 375m);
        receivable.TenantId = TenantId;
        typeof(Receivable).GetProperty(nameof(Receivable.Currency))!.SetValue(receivable, Currency.Create("CZK", "Kč", "Czech koruna"));
        return receivable;
    }

    private void CustomerHoldsCard(int expYear, string consentTextVersion = CardGuaranteeConsent)
    {
        var card = SavedCard.Start(_receivable.UserId, _receivable.CurrencyId, "cus_owing", null, null);
        typeof(SavedCard).GetProperty(nameof(SavedCard.ConsentTextVersion))!.SetValue(card, consentTextVersion);
        card.Capture("pm_owing", "visa", "4242", 12, expYear);
        _savedCards
            .Setup(r => r.GetCapturedForUserInCurrencyAsync(_receivable.UserId, _receivable.CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([card]);
    }

    [Fact]
    public async Task Switched_Off_It_Reads_And_Charges_Nothing()
    {
        _payments.SetupGet(p => p.OffSessionChargesEnabled).Returns(false);
        CustomerHoldsCard(DateTime.UtcNow.Year + 2);

        var result = await Handler().Handle(new ChargeOpenReceivables.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ChargeOpenReceivables.Response(0, 0), result.Value);
        Assert.Equal(0, _receivable.Attempts);
        _receivables.VerifyNoOtherCalls();
        _stripeFactory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Switched_On_An_Open_Receivable_Is_Charged_Once_To_The_Customers_Card_After_Its_Attempt_Is_Committed()
    {
        CustomerHoldsCard(DateTime.UtcNow.Year + 2);

        var result = await Handler().Handle(new ChargeOpenReceivables.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ChargeOpenReceivables.Response(1, 1), result.Value);
        _stripe.Verify(c => c.ChargeReceivableOffSessionAsync(
            _receivable.Id, 375m, "CZK", "cus_owing", "pm_owing", 1, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(new[] { "commit attempt 1", "charge" }, _sequence);
        _tenantProvider.Verify(t => t.SetTenantOverride(TenantId), Times.Once);
    }

    [Fact]
    public async Task A_Pay_Link_The_Customer_Holds_Is_Closed_Before_The_Charge()
    {
        CustomerHoldsCard(DateTime.UtcNow.Year + 2);
        _receivable.RecordPayLink("cs_held");
        _stripe
            .Setup(c => c.ExpireReceivableCheckoutSessionAsync("cs_held", It.IsAny<CancellationToken>()))
            .Callback(() => _sequence.Add("close pay link"))
            .ReturnsAsync(true);

        var result = await Handler().Handle(new ChargeOpenReceivables.Command(), CancellationToken.None);

        Assert.Equal(new ChargeOpenReceivables.Response(1, 1), result.Value);
        Assert.Equal(new[] { "commit attempt 1", "close pay link", "charge" }, _sequence);
    }

    [Fact]
    public async Task A_Receivable_Already_Paid_Through_Its_Pay_Link_Is_Not_Charged()
    {
        CustomerHoldsCard(DateTime.UtcNow.Year + 2);
        _receivable.RecordPayLink("cs_paid");
        _stripe
            .Setup(c => c.ExpireReceivableCheckoutSessionAsync("cs_paid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await Handler().Handle(new ChargeOpenReceivables.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ChargeOpenReceivables.Response(1, 0), result.Value);
        Assert.Equal(new[] { "commit attempt 1" }, _sequence);
    }

    [Fact]
    public async Task A_Customer_Whose_Card_Has_Expired_Is_Not_Charged_And_The_Attempt_Is_Counted()
    {
        CustomerHoldsCard(DateTime.UtcNow.Year - 1);

        var result = await Handler().Handle(new ChargeOpenReceivables.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ChargeOpenReceivables.Response(1, 0), result.Value);
        Assert.Equal(1, _receivable.Attempts);
        Assert.True(_receivable.IsOpen);
        _stripe.VerifyNoOtherCalls();
    }

    /// <summary>
    /// The consent printed with Save this card since <c>saved-card-draft-2026-10-05</c> promises the card is
    /// never charged unless the customer pays with it. Only the card-guarantee consents ever allowed a charge
    /// with the customer absent, so a card saved under the consent in force, or any later one, is not charged
    /// even with the switch on, and the receivable stays open for the customer's pay link.
    /// </summary>
    [Theory]
    [InlineData(SavedCard.ConsentTextVersionInForce)]
    [InlineData("saved-card-2026-11-02")]
    public async Task A_Card_Saved_Under_A_Consent_Other_Than_The_Card_Guarantee_Is_Never_Charged(string consentTextVersion)
    {
        CustomerHoldsCard(DateTime.UtcNow.Year + 2, consentTextVersion);

        var result = await Handler().Handle(new ChargeOpenReceivables.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ChargeOpenReceivables.Response(1, 0), result.Value);
        Assert.Equal(new[] { "commit attempt 1" }, _sequence);
        Assert.True(_receivable.IsOpen);
        _stripe.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Declined_Charge_Leaves_The_Receivable_Open_With_Its_Attempt_Counted()
    {
        CustomerHoldsCard(DateTime.UtcNow.Year + 2);
        _stripe
            .Setup(c => c.ChargeReceivableOffSessionAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("Your card was declined.")
            {
                StripeError = new StripeError { Type = "card_error", Code = "card_declined" },
            });

        var result = await Handler().Handle(new ChargeOpenReceivables.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ChargeOpenReceivables.Response(1, 0), result.Value);
        Assert.Equal(1, _receivable.Attempts);
        Assert.True(_receivable.IsOpen);
    }

    [Fact]
    public async Task With_Card_Payments_Switched_Off_It_Refuses_And_Charges_Nothing()
    {
        _stripeConfig.SetupGet(c => c.Enabled).Returns(false);
        CustomerHoldsCard(DateTime.UtcNow.Year + 2);

        var result = await Handler().Handle(new ChargeOpenReceivables.Command(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
        _receivables.VerifyNoOtherCalls();
        _stripe.VerifyNoOtherCalls();
    }
}
