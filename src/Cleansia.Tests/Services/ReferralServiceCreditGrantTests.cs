using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// Owner ruling 2026-10-04: a referral pays CREDIT, not points — the completing order's
/// <c>Currency.ReferralCredit</c> to the inviter and to the invited friend, each in that order's
/// currency, under one ledger key per side.
/// </summary>
public class ReferralServiceCreditGrantTests
{
    private const string OrderId = "order-referral-1";
    private const string ReferralId = "referral-1";
    private const string CzkId = "czk";
    private const string EurId = "eur";

    private readonly Mock<IReferralRepository> _referrals = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<ICreditAccountRepository> _credit = new();
    private readonly Dictionary<string, CreditAccount> _accounts = new();
    private readonly List<string> _ensured = [];
    private readonly HashSet<string> _erased = [];

    public ReferralServiceCreditGrantTests()
    {
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
    }

    private ReferralService Service() => new(
        Mock.Of<IReferralCodeRepository>(),
        _referrals.Object,
        _orders.Object,
        _credit.Object,
        Mock.Of<IUnitOfWork>(),
        NullLogger<ReferralService>.Instance);

    private static Currency NewCurrency(string id, string code, decimal? referralCredit)
    {
        var currency = Currency.Create(code, code, code);
        currency.Id = id;
        currency.SetReferralCredit(referralCredit);
        return currency;
    }

    private Referral Arrange(string referrerId, string referredId, Currency currency)
    {
        var order = OrderMockFactory.Generate(
            new OrderMockFactory.OrderPartial { Id = OrderId, UserId = referredId }, currency: currency);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        _orders.Setup(o => o.GetByIdForOwnerAsync(OrderId, referredId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _orders.Setup(o => o.GetQueryableForOwner(referredId))
            .Returns(new[] { order }.AsQueryable().BuildMock());

        var referral = Referral.CreateAccepted(referrerId, referredId, "code-1", "system");
        referral.Id = ReferralId;
        _referrals.Setup(r => r.GetForOrderOwnerAsync(OrderId, referredId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);
        return referral;
    }

    private CreditTransaction SingleGrant(string userId, string currencyId) =>
        Assert.Single(_accounts[$"{userId}:{currencyId}"].Transactions);

    [Fact]
    public async Task Both_Sides_Receive_The_Orders_Referral_Credit_In_The_Orders_Currency()
    {
        var referral = Arrange("referrer", "referred", NewCurrency(CzkId, "CZK", 150m));

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
        Assert.Equal(CzkId, referral.CreditCurrencyId);
        Assert.NotNull(referral.AwardedOn);
    }

    /// <summary>
    /// The figure and the account both follow the ORDER's currency: a friend who first books in euros
    /// pays both sides the euro figure, into euro balances, never the koruna one.
    /// </summary>
    [Fact]
    public async Task A_Euro_Order_Pays_The_Euro_Figure_Into_Euro_Accounts()
    {
        var referral = Arrange("referrer", "referred", NewCurrency(EurId, "EUR", 6m));

        await Service().ProcessOrderCompletedAsync(OrderId, "referred", CancellationToken.None);

        Assert.Equal(6m, SingleGrant("referrer", EurId).Amount);
        Assert.Equal(6m, SingleGrant("referred", EurId).Amount);
        Assert.Equal(EurId, referral.CreditCurrencyId);
        Assert.DoesNotContain(_accounts.Keys, key => key.EndsWith($":{CzkId}", StringComparison.Ordinal));
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
        Assert.Null(referral.CreditCurrencyId);
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
        Assert.Equal(150m, referral.CreditAwardedToReferred);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
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
