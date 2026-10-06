using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// Owner rulings 2026-10-04 and 2026-10-05: a referral pays CREDIT, not points, and each side is paid in the
/// currency it books in — the invited friend in the completing order's currency, the inviter in the currency
/// of their own most recent order of any status, or the friend's when they have never booked — each the
/// <c>Currency.ReferralCredit</c> of that side's currency, under one ledger key per side.
/// </summary>
public class ReferralServiceCreditGrantTests
{
    private const string OrderId = "order-referral-1";
    private const string ReferralId = "referral-1";
    private const string CzkId = "czk";
    private const string EurId = "eur";
    private const string PlnId = "pln";

    private readonly Mock<IReferralRepository> _referrals = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IReceivableRepository> _receivables = new();
    private readonly Mock<ICreditAccountRepository> _credit = new();
    private readonly Dictionary<string, CreditAccount> _accounts = new();
    private readonly List<string> _ensured = [];
    private readonly HashSet<string> _erased = [];
    private readonly List<Order> _friendsEarlierOrders = [];
    private readonly List<Receivable> _debts = [];

    public ReferralServiceCreditGrantTests()
    {
        _receivables.Setup(r => r.GetQueryableIgnoringTenant()).Returns(() => _debts.AsQueryable().BuildMock());
        _credit
            .Setup(c => c.EnsureForUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, string currencyId, CancellationToken _) =>
            {
                _ensured.Add(userId);
                if (_erased.Contains(userId))
                {
                    return null;
                }

                var key = $"{userId}:{currencyId}";
                return _accounts.TryGetValue(key, out var existing)
                    ? existing
                    : _accounts[key] = CreditAccount.Create(userId, currencyId, "system");
            });
        _referrals
            .Setup(r => r.GetContactFootprintAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) =>
                (Array.Empty<(string, string, string, string, string?)>(), Array.Empty<string>(), $"{userId}@cleansia.test"));
    }

    private ReferralService Service() => new(
        Mock.Of<IReferralCodeRepository>(),
        _referrals.Object,
        _orders.Object,
        _receivables.Object,
        _credit.Object,
        Mock.Of<IAdminNotifier>(),
        Mock.Of<IUnitOfWork>(),
        NullLogger<ReferralService>.Instance);

    private static Currency NewCurrency(string id, string code, decimal? referralCredit)
    {
        var currency = Currency.Create(code, code, code);
        currency.Id = id;
        currency.SetReferralCredit(referralCredit);
        return currency;
    }

    private Referral Arrange(string referrerId, string referredId, Currency currency, params (Currency Currency, int DaysAgo)[] referrerOrders)
    {
        var order = OrderMockFactory.Generate(
            new OrderMockFactory.OrderPartial { Id = OrderId, UserId = referredId }, currency: currency);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        _orders.Setup(o => o.GetByIdForOwnerAsync(OrderId, referredId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _orders.Setup(o => o.GetQueryableForOwner(referredId))
            .Returns(() => _friendsEarlierOrders.Append(order).AsQueryable().BuildMock());

        var booked = referrerOrders.Select(o =>
        {
            var row = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial { UserId = referrerId }, currency: o.Currency);
            row.Created("customer", DateTime.UtcNow.AddDays(-o.DaysAgo));
            return row;
        }).ToList();
        _orders.Setup(o => o.GetQueryableForOwner(referrerId)).Returns(booked.AsQueryable().BuildMock());

        var referral = Referral.CreateAccepted(referrerId, referredId, "code-1", "system");
        referral.Id = ReferralId;
        _referrals.Setup(r => r.GetForOrderOwnerAsync(OrderId, referredId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);
        return referral;
    }

    private CreditTransaction SingleGrant(string userId, string currencyId) =>
        Assert.Single(_accounts[$"{userId}:{currencyId}"].Transactions);

    private Order FriendCompletedEarlier(string orderId, Currency currency)
    {
        var earlier = OrderMockFactory.Generate(
            new OrderMockFactory.OrderPartial { Id = orderId, UserId = "referred", CurrentStatus = OrderStatus.Completed },
            currency: currency);
        _friendsEarlierOrders.Add(earlier);
        return earlier;
    }

    private void NotPaidAtTheDoor(Order order, ReceivableStatus status)
    {
        var debt = Receivable.ForUnpaidCash(order);
        switch (status)
        {
            case ReceivableStatus.Paid:
                debt.MarkPaid(stripePaymentIntentId: null, DateTimeOffset.UtcNow);
                break;
            case ReceivableStatus.WrittenOff:
                debt.WriteOff("admin", "reported in error", DateTimeOffset.UtcNow);
                break;
        }

        _debts.Add(debt);
    }

    [Fact]
    public async Task Both_Sides_Receive_The_Orders_Referral_Credit_When_Both_Book_In_Its_Currency()
    {
        var czk = NewCurrency(CzkId, "CZK", 150m);
        var referral = Arrange("referrer", "referred", czk, (czk, 10));

        await Service().ProcessOrderCompletedAsync(OrderId, "referred", CancellationToken.None);

        var toReferrer = SingleGrant("referrer", CzkId);
        Assert.Equal(150m, toReferrer.Amount);
        Assert.Equal(CreditTransactionReason.Referral, toReferrer.Reason);
        Assert.Equal($"referral:{ReferralId}:referrer", toReferrer.IdempotencyKey);
        Assert.Equal(OrderId, toReferrer.OrderId);

        var toReferred = SingleGrant("referred", CzkId);
        Assert.Equal(150m, toReferred.Amount);
        Assert.Equal(CreditTransactionReason.Referral, toReferred.Reason);
        Assert.Equal($"referral:{ReferralId}:referred", toReferred.IdempotencyKey);

        Assert.Equal(ReferralStatus.Qualified, referral.Status);
        Assert.Equal(OrderId, referral.FirstQualifyingOrderId);
        Assert.Equal(150m, referral.CreditAwardedToReferrer);
        Assert.Equal(150m, referral.CreditAwardedToReferred);
        Assert.Equal(CzkId, referral.ReferrerCreditCurrencyId);
        Assert.Equal(CzkId, referral.ReferredCreditCurrencyId);
        Assert.NotNull(referral.AwardedOn);
    }

    /// <summary>
    /// The friend first books in euros; the inviter's own latest booking is in koruna. Each is paid the
    /// figure of the currency they book in, into a balance in that currency.
    /// </summary>
    [Fact]
    public async Task Each_Side_Is_Paid_The_Figure_Of_The_Currency_It_Books_In()
    {
        var czk = NewCurrency(CzkId, "CZK", 150m);
        var eur = NewCurrency(EurId, "EUR", 6m);
        var referral = Arrange("referrer", "referred", eur, (eur, 40), (czk, 3));

        await Service().ProcessOrderCompletedAsync(OrderId, "referred", CancellationToken.None);

        Assert.Equal(150m, SingleGrant("referrer", CzkId).Amount);
        Assert.Equal(6m, SingleGrant("referred", EurId).Amount);
        Assert.Equal(["referred:eur", "referrer:czk"], _accounts.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(CzkId, referral.ReferrerCreditCurrencyId);
        Assert.Equal(EurId, referral.ReferredCreditCurrencyId);
        Assert.Equal(150m, referral.CreditAwardedToReferrer);
        Assert.Equal(6m, referral.CreditAwardedToReferred);
    }

    [Fact]
    public async Task An_Inviter_Who_Never_Booked_Is_Paid_In_The_Friends_Currency()
    {
        var referral = Arrange("referrer", "referred", NewCurrency(EurId, "EUR", 6m));

        await Service().ProcessOrderCompletedAsync(OrderId, "referred", CancellationToken.None);

        Assert.Equal(6m, SingleGrant("referrer", EurId).Amount);
        Assert.Equal(6m, SingleGrant("referred", EurId).Amount);
        Assert.Equal(EurId, referral.ReferrerCreditCurrencyId);
        Assert.Equal(EurId, referral.ReferredCreditCurrencyId);
        Assert.DoesNotContain(_accounts.Keys, key => key.EndsWith($":{CzkId}", StringComparison.Ordinal));
    }

    /// <summary>
    /// A side whose currency carries no referral figure is paid nothing; the other side is still paid in its
    /// own currency, and the referral still qualifies — whichever side lacks the figure, and whichever side's
    /// grant is reached first in the ordinal lock order.
    /// </summary>
    [Theory]
    [InlineData("user-a", "user-b", false)]
    [InlineData("user-b", "user-a", false)]
    [InlineData("user-a", "user-b", true)]
    [InlineData("user-b", "user-a", true)]
    public async Task A_Side_Whose_Currency_Has_No_Figure_Gets_Nothing_And_The_Other_Is_Still_Paid(
        string referrerId, string referredId, bool friendLacksTheFigure)
    {
        var czk = NewCurrency(CzkId, "CZK", 150m);
        var pln = NewCurrency(PlnId, "PLN", null);
        var referral = friendLacksTheFigure
            ? Arrange(referrerId, referredId, pln, (czk, 5))
            : Arrange(referrerId, referredId, czk, (pln, 5));
        var paidId = friendLacksTheFigure ? referrerId : referredId;

        await Service().ProcessOrderCompletedAsync(OrderId, referredId, CancellationToken.None);

        Assert.Equal([paidId], _ensured);
        Assert.Equal(150m, SingleGrant(paidId, CzkId).Amount);
        Assert.Equal(friendLacksTheFigure ? null : 150m, referral.CreditAwardedToReferred);
        Assert.Equal(friendLacksTheFigure ? null : CzkId, referral.ReferredCreditCurrencyId);
        Assert.Equal(friendLacksTheFigure ? 150m : null, referral.CreditAwardedToReferrer);
        Assert.Equal(friendLacksTheFigure ? CzkId : null, referral.ReferrerCreditCurrencyId);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
        Assert.NotNull(referral.AwardedOn);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task A_Currency_With_No_Referral_Credit_Qualifies_And_Pays_Nothing(int? figure)
    {
        var referral = Arrange("referrer", "referred", NewCurrency(CzkId, "CZK", figure));

        await Service().ProcessOrderCompletedAsync(OrderId, "referred", CancellationToken.None);

        Assert.Equal(ReferralStatus.Qualified, referral.Status);
        Assert.Null(referral.CreditAwardedToReferrer);
        Assert.Null(referral.CreditAwardedToReferred);
        Assert.Null(referral.ReferrerCreditCurrencyId);
        Assert.Null(referral.ReferredCreditCurrencyId);
        Assert.Null(referral.AwardedOn);
        Assert.Empty(_ensured);
    }

    [Fact]
    public async Task An_Erased_Side_Receives_Nothing_And_The_Other_Is_Still_Paid()
    {
        _erased.Add("referrer");
        var referral = Arrange("referrer", "referred", NewCurrency(CzkId, "CZK", 150m));

        await Service().ProcessOrderCompletedAsync(OrderId, "referred", CancellationToken.None);

        Assert.Equal(150m, SingleGrant("referred", CzkId).Amount);
        Assert.Null(referral.CreditAwardedToReferrer);
        Assert.Null(referral.ReferrerCreditCurrencyId);
        Assert.Equal(150m, referral.CreditAwardedToReferred);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
    }

    /// <summary>
    /// A write to a frozen company's books fails the whole commit, and this one is the completing order's —
    /// another, active company's. The side whose account sits on the frozen books is paid nothing and no
    /// account is opened for it; the other side is paid and the referral qualifies. The books asked about are
    /// those of the side's OWN currency.
    /// </summary>
    [Fact]
    public async Task A_Side_On_A_Frozen_Companys_Books_In_Its_Own_Currency_Receives_Nothing_And_The_Other_Is_Still_Paid()
    {
        var czk = NewCurrency(CzkId, "CZK", 150m);
        _credit
            .Setup(c => c.IsOnFrozenCompanyBooksAsync("referrer", CzkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var referral = Arrange("referrer", "referred", NewCurrency(EurId, "EUR", 6m), (czk, 2));

        await Service().ProcessOrderCompletedAsync(OrderId, "referred", CancellationToken.None);

        Assert.Equal(["referred"], _ensured);
        Assert.Equal(6m, SingleGrant("referred", EurId).Amount);
        Assert.Null(referral.CreditAwardedToReferrer);
        Assert.Equal(6m, referral.CreditAwardedToReferred);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
    }

    /// <summary>
    /// The friend's earlier booking was not paid to the cleaner at the door. Whether that debt is still open, has
    /// been paid since or was written off, the booking is never their first completion, so this later one
    /// qualifies the referral and pays both sides against it.
    /// </summary>
    [Theory]
    [InlineData(ReceivableStatus.Open)]
    [InlineData(ReceivableStatus.Paid)]
    [InlineData(ReceivableStatus.WrittenOff)]
    public async Task A_Booking_Not_Paid_At_The_Door_Is_Not_The_Friends_First_Completion(ReceivableStatus debtStatus)
    {
        var czk = NewCurrency(CzkId, "CZK", 150m);
        var referral = Arrange("referrer", "referred", czk);
        NotPaidAtTheDoor(FriendCompletedEarlier("order-unpaid-at-door", czk), debtStatus);

        await Service().ProcessOrderCompletedAsync(OrderId, "referred", CancellationToken.None);

        Assert.Equal(ReferralStatus.Qualified, referral.Status);
        Assert.Equal(OrderId, referral.FirstQualifyingOrderId);
        Assert.Equal(OrderId, SingleGrant("referrer", CzkId).OrderId);
        Assert.Equal(150m, SingleGrant("referrer", CzkId).Amount);
        Assert.Equal(150m, SingleGrant("referred", CzkId).Amount);
    }

    /// <summary>
    /// An earlier booking that was paid is a completion before this one, whether or not another booking went
    /// unpaid at the door: the referral keeps waiting and nobody is paid.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_Earlier_Paid_Completion_Leaves_The_Referral_Waiting(bool anotherNotPaidAtTheDoor)
    {
        var czk = NewCurrency(CzkId, "CZK", 150m);
        var referral = Arrange("referrer", "referred", czk);
        FriendCompletedEarlier("order-paid-earlier", czk);
        if (anotherNotPaidAtTheDoor)
        {
            NotPaidAtTheDoor(FriendCompletedEarlier("order-unpaid-at-door", czk), ReceivableStatus.Paid);
        }

        await Service().ProcessOrderCompletedAsync(OrderId, "referred", CancellationToken.None);

        Assert.Equal(ReferralStatus.Accepted, referral.Status);
        Assert.Null(referral.FirstQualifyingOrderId);
        Assert.Empty(_ensured);
    }

    [Fact]
    public async Task A_Second_Completion_Pays_Nobody_Again()
    {
        Arrange("referrer", "referred", NewCurrency(CzkId, "CZK", 150m));

        await Service().ProcessOrderCompletedAsync(OrderId, "referred", CancellationToken.None);
        await Service().ProcessOrderCompletedAsync(OrderId, "referred", CancellationToken.None);

        Assert.Single(_accounts["referrer:czk"].Transactions);
        Assert.Single(_accounts["referred:czk"].Transactions);
        Assert.Equal(2, _ensured.Count);
    }

    /// <summary>
    /// Each grant holds its owner's credit lock until commit, so the two locks are taken in ordinal user-id
    /// order whichever side that is — two completions paying the same pair the other way round then queue
    /// instead of deadlocking.
    /// </summary>
    [Theory]
    [InlineData("user-a", "user-b")]
    [InlineData("user-b", "user-a")]
    public async Task The_Owner_Locks_Are_Taken_In_Ordinal_User_Order(string referrerId, string referredId)
    {
        Arrange(referrerId, referredId, NewCurrency(CzkId, "CZK", 150m));

        await Service().ProcessOrderCompletedAsync(OrderId, referredId, CancellationToken.None);

        Assert.Equal(["user-a", "user-b"], _ensured);
    }
}
