using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Referrals.Admin;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Referrals.Admin;

/// <summary>
/// An Accepted referral the admin deems legitimate is qualified and both sides are credited the way the
/// automatic path credits them (owner ruling 2026-10-04): the referral credit of the referred customer's
/// market currency — the currency of their latest order — or of the platform default when they have
/// never booked. A retry on the already-Qualified row is a guarded no-op.
/// </summary>
public class ForceQualifyReferralHandlerTests
{
    private const string ReferralId = "ref-2";
    private const string ReferrerUserId = "referrer-2";
    private const string ReferredUserId = "referred-2";
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
                It.IsAny<Referral>(), It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Referral _, string _, decimal? amount, string? _, string _, string? _, CancellationToken _) =>
                (amount, amount));
        BookedOrders();
    }

    private static Currency NewCurrency(string id, string code, decimal referralCredit)
    {
        var currency = Currency.Create(code, code, code);
        currency.Id = id;
        currency.SetReferralCredit(referralCredit);
        return currency;
    }

    private void BookedOrders(params (Currency Currency, DateTime CreatedOn)[] orders)
    {
        var rows = orders.Select(o =>
        {
            var order = OrderMockFactory.Generate(
                new OrderMockFactory.OrderPartial { UserId = ReferredUserId }, currency: o.Currency);
            order.Created("customer", o.CreatedOn);
            return order;
        }).ToList();
        _orders.Setup(r => r.GetQueryableForOwner(ReferredUserId)).Returns(rows.AsQueryable().BuildMock());
    }

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

    [Fact]
    public async Task Both_Sides_Are_Credited_In_The_Currency_Of_The_Referred_Customers_Latest_Order()
    {
        var referral = AcceptedReferral();
        BookedOrders((_czk, DateTime.UtcNow.AddDays(-30)), (_eur, DateTime.UtcNow.AddDays(-2)));

        var result = await CreateHandler().Handle(new ForceQualifyReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ForceQualifyReferral.Response(ReferralId, 6m, 6m, "EUR"), result.Value);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
        Assert.Equal("eur", referral.CreditCurrencyId);
        Assert.Equal(6m, referral.CreditAwardedToReferrer);
        Assert.Equal(6m, referral.CreditAwardedToReferred);
        Assert.Null(referral.FirstQualifyingOrderId);
        _referralService.Verify(s => s.AwardCreditAsync(
            referral, "eur", 6m, null, ActorId, Reason, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Referred_Customer_Who_Never_Booked_Is_Credited_In_The_Platform_Default()
    {
        var referral = AcceptedReferral();

        var result = await CreateHandler().Handle(new ForceQualifyReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ForceQualifyReferral.Response(ReferralId, 150m, 150m, "CZK"), result.Value);
        Assert.Equal("czk", referral.CreditCurrencyId);
    }

    [Fact]
    public async Task An_Erased_Side_Is_Reported_As_Receiving_Nothing()
    {
        var referral = AcceptedReferral();
        _referralService
            .Setup(s => s.AwardCreditAsync(
                referral, "czk", 150m, null, ActorId, Reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((decimal?)null, (decimal?)150m));

        var result = await CreateHandler().Handle(new ForceQualifyReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.Equal(new ForceQualifyReferral.Response(ReferralId, 0m, 150m, "CZK"), result.Value);
        Assert.Null(referral.CreditAwardedToReferrer);
        Assert.Equal(150m, referral.CreditAwardedToReferred);
    }

    [Fact]
    public async Task ForceQualify_RunTwice_SecondIsGuardedNoOp_NoDoubleGrant()
    {
        AcceptedReferral();

        var first = await CreateHandler().Handle(new ForceQualifyReferral.Command(ReferralId, Reason), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await CreateHandler().Handle(new ForceQualifyReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal(BusinessErrorMessage.ReferralNotAccepted, second.Error!.Message);
        Assert.Equal(nameof(ForceQualifyReferral.Command.ReferralId), second.Error.Code);
        _referralService.Verify(s => s.AwardCreditAsync(
            It.IsAny<Referral>(), It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForceQualify_Handler_DoesNotCommit()
    {
        AcceptedReferral();

        await CreateHandler().Handle(new ForceQualifyReferral.Command(ReferralId, Reason), CancellationToken.None);

        _referralRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
