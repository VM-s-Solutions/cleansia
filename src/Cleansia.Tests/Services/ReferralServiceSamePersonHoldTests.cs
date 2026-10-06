using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// Owner ruling 2026-10-05: when the friend's first booking completes, the two accounts' contact details are
/// compared, and a referral whose accounts appear to be one person — a shared home, a shared phone number or a
/// shared inbox — pays nothing: it stays Accepted, held with the reasons, never expires, and the company's
/// administrators who can intervene are told once. The comparison normalises what customers type differently
/// (case, accents, spacing, the ZIP's space, the flat's spacing, a national prefix, a Gmail alias) and spares
/// neighbours on recorded, different flats.
/// </summary>
public class ReferralServiceSamePersonHoldTests
{
    private const string OrderId = "order-hold-1";
    private const string ReferralId = "referral-hold-1";
    private const string ReferrerId = "referrer";
    private const string ReferredId = "referred";
    private const string CompanyId = "tenant-hold";
    private const string Czechia = "country-cz";

    private readonly Mock<IReferralRepository> _referrals = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<ICreditAccountRepository> _credit = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private readonly List<AdminEvent> _told = [];
    private readonly List<CreditAccount> _accounts = [];
    private readonly ReferralCode _code = ReferralCode.Generate(ReferrerId, "HOLD01", "system");
    private readonly Referral _referral;

    private readonly List<(string, string, string, string, string?)> _referrerAddresses = [];
    private readonly List<(string, string, string, string, string?)> _referredAddresses = [];
    private readonly List<string> _referrerPhones = [];
    private readonly List<string> _referredPhones = [];
    private string? _referrerEmail = "inviter@seznam.cz";
    private string? _referredEmail = "friend@email.cz";

    public ReferralServiceSamePersonHoldTests()
    {
        BookIn(NewCurrency("czk", "CZK", 150m), inviter: null);

        _referral = Referral.CreateAccepted(ReferrerId, ReferredId, _code.Id, "system");
        _referral.Id = ReferralId;
        _referral.TenantId = CompanyId;
        typeof(Referral).GetProperty(nameof(Referral.ReferralCode))!.SetValue(_referral, _code);
        _referrals.Setup(r => r.GetForOrderOwnerAsync(OrderId, ReferredId, It.IsAny<CancellationToken>())).ReturnsAsync(_referral);
        _referrals
            .Setup(r => r.GetContactFootprintAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) =>
            {
                if (userId == ReferrerId)
                {
                    return (_referrerAddresses, _referrerPhones, _referrerEmail);
                }

                return (_referredAddresses, _referredPhones, _referredEmail);
            });

        _credit
            .Setup(c => c.EnsureForUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, string currencyId, CancellationToken _) =>
            {
                var account = CreditAccount.Create(userId, currencyId, "system");
                _accounts.Add(account);
                return account;
            });
        _adminNotifier
            .Setup(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEvent, CancellationToken>((adminEvent, _) => _told.Add(adminEvent))
            .Returns(Task.CompletedTask);
    }

    private static Currency NewCurrency(string id, string code, decimal? referralCredit)
    {
        var currency = Currency.Create(code, code, code);
        currency.Id = id;
        currency.SetReferralCredit(referralCredit);
        return currency;
    }

    /// <summary>The friend's completing order in <paramref name="friend"/>; the inviter's one booking, if any.</summary>
    private void BookIn(Currency friend, Currency? inviter)
    {
        var order = OrderMockFactory.Generate(
            new OrderMockFactory.OrderPartial { Id = OrderId, UserId = ReferredId }, currency: friend);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        _orders.Setup(o => o.GetByIdForOwnerAsync(OrderId, ReferredId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _orders.Setup(o => o.GetQueryableForOwner(ReferredId)).Returns(new[] { order }.AsQueryable().BuildMock());
        Order[] own = inviter is null
            ? []
            : [OrderMockFactory.Generate(new OrderMockFactory.OrderPartial { UserId = ReferrerId }, currency: inviter)];
        _orders.Setup(o => o.GetQueryableForOwner(ReferrerId)).Returns(own.AsQueryable().BuildMock());
    }

    private ReferralService Service() => new(
        Mock.Of<IReferralCodeRepository>(),
        _referrals.Object,
        _orders.Object,
        Mock.Of<IReceivableRepository>(),
        _credit.Object,
        _adminNotifier.Object,
        Mock.Of<IUnitOfWork>(),
        NullLogger<ReferralService>.Instance);

    private Task CompleteAsync() => Service().ProcessOrderCompletedAsync(OrderId, ReferredId, CancellationToken.None);

    private static (string, string, string, string, string?) At(
        string street, string city, string zip, string? flat = null, string country = Czechia) =>
        (country, zip, city, street, flat);

    private void AssertHeld(string reasons)
    {
        Assert.Equal(ReferralStatus.Accepted, _referral.Status);
        Assert.Equal(reasons, _referral.HoldReasons);
        Assert.Equal(OrderId, _referral.FirstQualifyingOrderId);
        Assert.Null(_referral.CreditAwardedToReferrer);
        Assert.Null(_referral.CreditAwardedToReferred);
        Assert.Null(_referral.AwardedOn);
        Assert.Empty(_accounts);
        Assert.Equal(0, _code.TimesUsed);

        var told = Assert.Single(_told);
        Assert.Equal(AdminNotificationEventCatalog.ReferralHeld, told.Key);
        Assert.Equal(CompanyId, told.TenantId);
        Assert.Equal(ReferralId, told.Subject);
        Assert.Equal(new Dictionary<string, string> { ["referralId"] = ReferralId }, told.Args);
    }

    private void AssertPaid()
    {
        Assert.Equal(ReferralStatus.Qualified, _referral.Status);
        Assert.Null(_referral.HoldReasons);
        Assert.Equal(150m, _referral.CreditAwardedToReferrer);
        Assert.Equal(150m, _referral.CreditAwardedToReferred);
        Assert.Equal(2, _accounts.Count);
        Assert.Equal(1, _code.TimesUsed);
        Assert.Empty(_told);
    }

    /// <summary>
    /// The same home typed twice — lower case, no accents, a trailing space, the ZIP without its space — is
    /// one home, though each spelling has its own address row.
    /// </summary>
    [Fact]
    public async Task The_Same_Home_Typed_Differently_Holds_The_Referral_For_Its_Address()
    {
        _referrerAddresses.Add(At("Vinohradská 12", "Praha", "120 00"));
        _referredAddresses.Add(At("vinohradska  12 ", "praha", "12000"));

        await CompleteAsync();

        AssertHeld(Referral.HoldReasonAddress);
    }

    [Theory]
    [InlineData("3A", "3 a")]
    [InlineData("3A", null)]
    [InlineData(null, null)]
    [InlineData("Byt 4", "byt4")]
    public async Task One_Address_With_The_Same_Flat_Or_A_Flat_Recorded_On_At_Most_One_Side_Is_Held(string? referrerFlat, string? referredFlat)
    {
        _referrerAddresses.Add(At("Vinohradská 12", "Praha", "120 00", referrerFlat));
        _referredAddresses.Add(At("Vinohradská 12", "Praha", "120 00", referredFlat));

        await CompleteAsync();

        AssertHeld(Referral.HoldReasonAddress);
    }

    [Fact]
    public async Task Neighbours_On_Two_Recorded_Flats_Of_One_Building_Are_Paid()
    {
        _referrerAddresses.Add(At("Vinohradská 12", "Praha", "120 00", "3A"));
        _referredAddresses.Add(At("Vinohradská 12", "Praha", "120 00", "5"));

        await CompleteAsync();

        AssertPaid();
    }

    [Theory]
    [InlineData("Vinohradská 14", "Praha", "120 00", Czechia)]
    [InlineData("Vinohradská 12", "Brno", "120 00", Czechia)]
    [InlineData("Vinohradská 12", "Praha", "120 01", Czechia)]
    [InlineData("Vinohradská 12", "Praha", "120 00", "country-sk")]
    public async Task A_Different_Street_City_Zip_Or_Country_Is_Paid(string street, string city, string zip, string country)
    {
        _referrerAddresses.Add(At("Vinohradská 12", "Praha", "120 00"));
        _referredAddresses.Add(At(street, city, zip, country: country));

        await CompleteAsync();

        AssertPaid();
    }

    [Theory]
    [InlineData("+420 777 123 456", "777123456")]
    [InlineData("00420777123456", "+420-777-123-456")]
    [InlineData("777 123 456", "(+420) 777 123 456")]
    public async Task One_Phone_Number_Typed_Differently_Holds_The_Referral(string referrerPhone, string referredPhone)
    {
        _referrerPhones.Add(referrerPhone);
        _referredPhones.Add(referredPhone);

        await CompleteAsync();

        AssertHeld(Referral.HoldReasonPhone);
    }

    /// <summary>
    /// An erased order's phone holds no digits and a short number cannot tell two people apart: neither is
    /// compared, even when both sides carry the same one.
    /// </summary>
    [Theory]
    [InlineData("[DELETED]")]
    [InlineData("12345678")]
    [InlineData("")]
    public async Task A_Phone_With_Fewer_Than_Nine_Digits_Is_Not_Compared(string phone)
    {
        _referrerPhones.Add(phone);
        _referredPhones.Add(phone);

        await CompleteAsync();

        AssertPaid();
    }

    [Fact]
    public async Task Two_Different_Phone_Numbers_Are_Paid()
    {
        _referrerPhones.Add("+420 777 123 456");
        _referredPhones.Add("+420 777 123 457");

        await CompleteAsync();

        AssertPaid();
    }

    [Theory]
    [InlineData("a+1@gmail.com", "a+2@gmail.com")]
    [InlineData("j.novak@gmail.com", "jnovak@googlemail.com")]
    [InlineData("Jan.Novak@Seznam.cz", "jan.novak+rewards@seznam.cz")]
    public async Task Two_Addresses_Of_One_Inbox_Hold_The_Referral(string referrerEmail, string referredEmail)
    {
        _referrerEmail = referrerEmail;
        _referredEmail = referredEmail;

        await CompleteAsync();

        AssertHeld(Referral.HoldReasonEmail);
    }

    [Theory]
    [InlineData("jan.novak@seznam.cz", "jannovak@seznam.cz")]
    [InlineData("jan@gmail.com", "jan@seznam.cz")]
    [InlineData("deleted_a@anonymized.local", "deleted_a@anonymized.local")]
    public async Task Different_Inboxes_And_Erased_Addresses_Are_Paid(string referrerEmail, string referredEmail)
    {
        _referrerEmail = referrerEmail;
        _referredEmail = referredEmail;

        await CompleteAsync();

        AssertPaid();
    }

    [Fact]
    public async Task Every_Signal_That_Matches_Is_Recorded()
    {
        _referrerAddresses.Add(At("Vinohradská 12", "Praha", "120 00"));
        _referredAddresses.Add(At("Vinohradská 12", "Praha", "120 00"));
        _referrerPhones.Add("777123456");
        _referredPhones.Add("+420777123456");
        _referrerEmail = "a+1@gmail.com";
        _referredEmail = "a@gmail.com";

        await CompleteAsync();

        AssertHeld("address,phone,email");
    }

    [Fact]
    public async Task Two_Accounts_With_Nothing_In_Common_Are_Paid()
    {
        _referrerAddresses.Add(At("Vinohradská 12", "Praha", "120 00", "3"));
        _referredAddresses.Add(At("Korunní 5", "Praha", "120 00", "3"));
        _referrerPhones.Add("+420 777 123 456");
        _referredPhones.Add("+420 606 654 321");

        await CompleteAsync();

        AssertPaid();
    }

    /// <summary>
    /// A replayed completion of the held order finds the referral held and does nothing: no second hold, no
    /// second notice (the second notice's outbox key would collide and fail the commit), no payment.
    /// </summary>
    [Fact]
    public async Task A_Second_Completion_Of_A_Held_Referral_Changes_Nothing_And_Tells_Nobody_Again()
    {
        _referrerAddresses.Add(At("Vinohradská 12", "Praha", "120 00"));
        _referredAddresses.Add(At("Vinohradská 12", "Praha", "120 00"));
        await CompleteAsync();

        await CompleteAsync();

        AssertHeld(Referral.HoldReasonAddress);
        _referrals.Verify(r => r.GetContactFootprintAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task A_Completion_After_The_Window_Expires_The_Referral_Instead_Of_Holding_It()
    {
        _referrerAddresses.Add(At("Vinohradská 12", "Praha", "120 00"));
        _referredAddresses.Add(At("Vinohradská 12", "Praha", "120 00"));
        typeof(Referral).GetProperty(nameof(Referral.AcceptedOn))!.SetValue(_referral, DateTimeOffset.UtcNow.AddDays(-91));

        await CompleteAsync();

        Assert.Equal(ReferralStatus.Expired, _referral.Status);
        Assert.Null(_referral.HoldReasons);
        Assert.Empty(_told);
        Assert.Empty(_accounts);
    }

    /// <summary>
    /// Neither side books in a currency with a referral figure: a release and a rejection would both pay
    /// nothing, so there is nothing for an administrator to review. The accounts are not compared, nobody is
    /// told, and the referral qualifies with nothing paid, as it does for two strangers.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Shared_Home_On_A_Referral_That_Can_Pay_Neither_Side_Qualifies_With_Nothing_Paid_And_Tells_Nobody(bool inviterBooked)
    {
        BookIn(NewCurrency("pln", "PLN", null), inviter: inviterBooked ? NewCurrency("eur", "EUR", 0m) : null);
        _referrerAddresses.Add(At("Vinohradská 12", "Praha", "120 00"));
        _referredAddresses.Add(At("Vinohradská 12", "Praha", "120 00"));

        await CompleteAsync();

        Assert.Equal(ReferralStatus.Qualified, _referral.Status);
        Assert.Null(_referral.HoldReasons);
        Assert.Equal(OrderId, _referral.FirstQualifyingOrderId);
        Assert.Null(_referral.CreditAwardedToReferrer);
        Assert.Null(_referral.CreditAwardedToReferred);
        Assert.Null(_referral.AwardedOn);
        Assert.Empty(_accounts);
        Assert.Empty(_told);
        _referrals.Verify(r => r.GetContactFootprintAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>One side has a figure to be paid: the shared home holds the referral as it does when both have.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Shared_Home_Holds_The_Referral_When_Either_Side_Could_Be_Paid(bool friendHasTheFigure)
    {
        var figured = NewCurrency("czk", "CZK", 150m);
        var unfigured = NewCurrency("pln", "PLN", null);
        BookIn(friendHasTheFigure ? figured : unfigured, inviter: friendHasTheFigure ? unfigured : figured);
        _referrerAddresses.Add(At("Vinohradská 12", "Praha", "120 00"));
        _referredAddresses.Add(At("Vinohradská 12", "Praha", "120 00"));

        await CompleteAsync();

        AssertHeld(Referral.HoldReasonAddress);
    }
}
