using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Referrals.Admin;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Referrals.Admin;

/// <summary>
/// An Accepted referral the admin deems legitimate — or a held one the admin releases — is qualified and each
/// side is credited the way the automatic path credits it (owner rulings 2026-10-04 and 2026-10-05): the friend
/// in the currency of the held order, else of their latest order, else the platform default; the inviter in the
/// currency of their own latest order, else the friend's. A retry on the already-Qualified row is a guarded no-op,
/// and an action sent for a hold state the row no longer has is refused.
/// </summary>
public class ForceQualifyReferralHandlerTests
{
    private const string ReferralId = "ref-2";
    private const string ReferrerUserId = "referrer-2";
    private const string ReferredUserId = "referred-2";
    private const string HeldOrderId = "order-held-2";
    private const string ActorId = "admin-1";
    private const string Reason = "qualifying order completed but auto-path missed it";

    private readonly Mock<IReferralRepository> _referralRepository = new();
    private readonly Mock<IReferralService> _referralService = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly Mock<IUserSessionProvider> _userSession = new();
    private readonly Currency _czk = NewCurrency("czk", "CZK", 150m);
    private readonly Currency _eur = NewCurrency("eur", "EUR", 6m);

    public ForceQualifyReferralHandlerTests()
    {
        _userSession.Setup(s => s.GetUserId()).Returns(ActorId);
        _currencies.Setup(c => c.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_czk);
        _currencies.Setup(c => c.GetByIdAsync(_czk.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_czk);
        _currencies.Setup(c => c.GetByIdAsync(_eur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_eur);
        _referralService
            .Setup(s => s.AwardCreditAsync(
                It.IsAny<Referral>(), It.IsAny<Currency?>(), It.IsAny<Currency?>(), It.IsAny<string?>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Referral _, Currency? referrer, Currency? referred, string? _, string _, string? _, CancellationToken _) =>
                (referrer?.ReferralCredit, referred?.ReferralCredit));
    }

    private static Currency NewCurrency(string id, string code, decimal referralCredit)
    {
        var currency = Currency.Create(code, code, code);
        currency.Id = id;
        currency.SetReferralCredit(referralCredit);
        return currency;
    }

    private void BooksIn(string userId, Currency currency) =>
        _referralService.Setup(s => s.GetBookingCurrencyAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(currency);

    private static readonly ForceQualifyReferral.Command ForceQualify = new(ReferralId, Reason, ExpectHeld: false);
    private static readonly ForceQualifyReferral.Command Release = new(ReferralId, Reason, ExpectHeld: true);

    private ForceQualifyReferral.Handler CreateHandler() => new(
        _referralRepository.Object, _referralService.Object, _orders.Object, _currencies.Object, _userSession.Object);

    private Referral AcceptedReferral()
    {
        var referral = Referral.CreateAccepted(ReferrerUserId, ReferredUserId, "code-2", "system");
        referral.Id = ReferralId;
        _referralRepository.Setup(r => r.GetByIdAsync(ReferralId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);
        return referral;
    }

    private Referral HeldReferral(Currency? heldOrderCurrency)
    {
        var referral = AcceptedReferral();
        referral.HoldForReview(HeldOrderId, Referral.HoldReasonAddress, "system");
        _orders.Setup(o => o.GetOwnerAndCurrencyAsync(HeldOrderId, ReferredUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldOrderCurrency is null ? null : new OrderOwnerAndCurrency(ReferredUserId, heldOrderCurrency.Id));
        return referral;
    }

    [Fact]
    public async Task Each_Side_Is_Credited_In_The_Currency_It_Books_In()
    {
        var referral = AcceptedReferral();
        BooksIn(ReferrerUserId, _czk);
        BooksIn(ReferredUserId, _eur);

        var result = await CreateHandler().Handle(ForceQualify, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ForceQualifyReferral.Response(ReferralId, 150m, "CZK", 6m, "EUR"), result.Value);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
        Assert.Equal("czk", referral.ReferrerCreditCurrencyId);
        Assert.Equal("eur", referral.ReferredCreditCurrencyId);
        Assert.Equal(150m, referral.CreditAwardedToReferrer);
        Assert.Equal(6m, referral.CreditAwardedToReferred);
        Assert.Null(referral.FirstQualifyingOrderId);
        _referralService.Verify(s => s.AwardCreditAsync(
            referral, _czk, _eur, null, ActorId, Reason, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_Inviter_Who_Never_Booked_Is_Credited_In_The_Friends_Currency()
    {
        var referral = AcceptedReferral();
        BooksIn(ReferredUserId, _eur);

        var result = await CreateHandler().Handle(ForceQualify, CancellationToken.None);

        Assert.Equal(new ForceQualifyReferral.Response(ReferralId, 6m, "EUR", 6m, "EUR"), result.Value);
        Assert.Equal("eur", referral.ReferrerCreditCurrencyId);
    }

    [Fact]
    public async Task Neither_Side_Having_Booked_Credits_Both_In_The_Platform_Default()
    {
        var referral = AcceptedReferral();

        var result = await CreateHandler().Handle(ForceQualify, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ForceQualifyReferral.Response(ReferralId, 150m, "CZK", 150m, "CZK"), result.Value);
        Assert.Equal("czk", referral.ReferrerCreditCurrencyId);
        Assert.Equal("czk", referral.ReferredCreditCurrencyId);
    }

    [Fact]
    public async Task An_Erased_Side_Is_Reported_As_Receiving_Nothing()
    {
        var referral = AcceptedReferral();
        _referralService
            .Setup(s => s.AwardCreditAsync(
                referral, _czk, _czk, null, ActorId, Reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((decimal?)null, (decimal?)150m));

        var result = await CreateHandler().Handle(ForceQualify, CancellationToken.None);

        Assert.Equal(new ForceQualifyReferral.Response(ReferralId, 0m, "CZK", 150m, "CZK"), result.Value);
        Assert.Null(referral.CreditAwardedToReferrer);
        Assert.Null(referral.ReferrerCreditCurrencyId);
        Assert.Equal(150m, referral.CreditAwardedToReferred);
    }

    /// <summary>
    /// Releasing a held referral pays the friend in the currency of the order that was held, not of whatever
    /// they booked since, and both grants name that order. The row keeps its order and its hold reasons.
    /// </summary>
    [Fact]
    public async Task Releasing_A_Held_Referral_Pays_The_Friend_In_The_Held_Orders_Currency_Against_That_Order()
    {
        var referral = HeldReferral(heldOrderCurrency: _eur);
        BooksIn(ReferredUserId, _czk);
        BooksIn(ReferrerUserId, _czk);

        var result = await CreateHandler().Handle(Release, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ForceQualifyReferral.Response(ReferralId, 150m, "CZK", 6m, "EUR"), result.Value);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
        Assert.Equal(HeldOrderId, referral.FirstQualifyingOrderId);
        Assert.Equal(Referral.HoldReasonAddress, referral.HoldReasons);
        Assert.Equal("eur", referral.ReferredCreditCurrencyId);
        _referralService.Verify(s => s.AwardCreditAsync(
            referral, _czk, _eur, HeldOrderId, ActorId, Reason, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The friend was erased while the referral was held: the held order no longer names them, and they have no
    /// bookings. The release does not fail; the inviter is paid in their own currency.
    /// </summary>
    [Fact]
    public async Task Releasing_After_The_Friend_Was_Erased_Pays_The_Inviter_In_Their_Own_Currency()
    {
        var referral = HeldReferral(heldOrderCurrency: null);
        BooksIn(ReferrerUserId, _eur);
        _referralService
            .Setup(s => s.AwardCreditAsync(referral, _eur, _czk, HeldOrderId, ActorId, Reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((decimal?)6m, (decimal?)null));

        var result = await CreateHandler().Handle(Release, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ForceQualifyReferral.Response(ReferralId, 6m, "EUR", 0m, "CZK"), result.Value);
        Assert.Equal("eur", referral.ReferrerCreditCurrencyId);
        Assert.Null(referral.ReferredCreditCurrencyId);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
    }

    /// <summary>
    /// The administrator acts on the row as the list showed it. A force-qualify sent from a row that was not
    /// held reaches a referral held since — the friend's first order completed while the dialog was open — and
    /// a release reaches one that is not held: either is refused, pays nobody and leaves the row as it is, so
    /// the hold's reasons are never paid past unseen.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_Action_Sent_For_A_Hold_State_The_Referral_No_Longer_Has_Is_Refused_And_Pays_Nobody(bool heldNow)
    {
        var referral = heldNow ? HeldReferral(heldOrderCurrency: _czk) : AcceptedReferral();
        BooksIn(ReferrerUserId, _czk);
        BooksIn(ReferredUserId, _czk);

        var result = await CreateHandler().Handle(heldNow ? ForceQualify : Release, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.ReferralHoldChanged, result.Error!.Message);
        Assert.Equal(nameof(ForceQualifyReferral.Command.ExpectHeld), result.Error.Code);
        Assert.Equal(ReferralStatus.Accepted, referral.Status);
        Assert.Null(referral.AwardedOn);
        _referralService.Verify(s => s.AwardCreditAsync(
            It.IsAny<Referral>(), It.IsAny<Currency?>(), It.IsAny<Currency?>(), It.IsAny<string?>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForceQualify_RunTwice_SecondIsGuardedNoOp_NoDoubleGrant()
    {
        AcceptedReferral();

        var first = await CreateHandler().Handle(ForceQualify, CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await CreateHandler().Handle(ForceQualify, CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal(BusinessErrorMessage.ReferralNotAccepted, second.Error!.Message);
        Assert.Equal(nameof(ForceQualifyReferral.Command.ReferralId), second.Error.Code);
        _referralService.Verify(s => s.AwardCreditAsync(
            It.IsAny<Referral>(), It.IsAny<Currency?>(), It.IsAny<Currency?>(), It.IsAny<string?>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForceQualify_Handler_DoesNotCommit()
    {
        AcceptedReferral();

        await CreateHandler().Handle(ForceQualify, CancellationToken.None);

        _referralRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
