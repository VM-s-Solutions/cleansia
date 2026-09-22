using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// A Stripe Customer per currency per user (owner ruling 2026-09-13): Stripe locks a
/// Customer to the currency of its first invoice, so the Customer a subscription is created on is
/// resolved per currency — an existing row, else the legacy Customer when it can only ever have
/// billed this currency, else a new Customer. The legacy field is still written on first creation.
/// </summary>
public class StripeCustomerResolverTests
{
    private const string UserId = "user-resolver-1";

    private readonly Mock<IUserStripeCustomerRepository> _customers = new();
    private readonly Mock<IUserMembershipRepository> _memberships = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly List<UserStripeCustomer> _added = [];
    private readonly Currency _czk = MembershipPricingMockFactory.Czk();
    private readonly Currency _eur = MembershipPricingMockFactory.Eur();

    public StripeCustomerResolverTests()
    {
        _customers.Setup(r => r.Add(It.IsAny<UserStripeCustomer>())).Callback<UserStripeCustomer>(_added.Add);
        _stripe
            .Setup(c => c.CreateCustomerAsync(UserId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cus_new");
    }

    private static User NewUser(string? legacyStripeCustomerId)
    {
        var user = User.CreateWithPassword("resolver@example.com", "12345678Test!", "Re", "Solver");
        user.Id = UserId;
        if (legacyStripeCustomerId is not null)
        {
            user.AssignStripeCustomerId(legacyStripeCustomerId);
        }

        return user;
    }

    private StripeCustomerResolver Resolver() =>
        new(_customers.Object, _memberships.Object, _stripe.Object, NullLogger<StripeCustomerResolver>.Instance);

    private void ArrangeRow(string currencyId, string stripeCustomerId) =>
        _customers
            .Setup(r => r.GetForUserInCurrencyAsync(UserId, currencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserStripeCustomer.Create(UserId, currencyId, stripeCustomerId));

    [Fact]
    public async Task A_Row_For_The_Currency_Is_Used_As_Is()
    {
        ArrangeRow(_eur.Id, "cus_eur");
        var user = NewUser("cus_legacy");

        var resolved = await Resolver().ResolveForCurrencyAsync(user, _eur, CancellationToken.None);

        Assert.Equal("cus_eur", resolved);
        Assert.Empty(_added);
        _stripe.Verify(c => c.CreateCustomerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("cus_legacy", user.StripeCustomerId);
    }

    /// <summary>
    /// The legacy Customer has only ever billed this currency (a cancelled CZK membership) or nothing at
    /// all: it is adopted for CZK — the row is written, no Stripe call.
    /// </summary>
    [Fact]
    public async Task The_Legacy_Customer_Is_Adopted_When_It_Cannot_Be_Locked_To_Another_Currency()
    {
        var user = NewUser("cus_legacy");
        _memberships
            .Setup(r => r.HasAnyInOtherCurrencyAsync(UserId, _czk.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var resolved = await Resolver().ResolveForCurrencyAsync(user, _czk, CancellationToken.None);

        Assert.Equal("cus_legacy", resolved);
        var row = Assert.Single(_added);
        Assert.Equal((UserId, _czk.Id, "cus_legacy"), (row.UserId, row.CurrencyId, row.StripeCustomerId));
        _stripe.Verify(c => c.CreateCustomerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The re-subscribe case the ruling exists for: a cancelled CZK Plus, now subscribing in EUR.</summary>
    [Fact]
    public async Task A_Legacy_Customer_That_Billed_Another_Currency_Gets_A_New_Customer_For_This_One()
    {
        var user = NewUser("cus_legacy");
        _memberships
            .Setup(r => r.HasAnyInOtherCurrencyAsync(UserId, _eur.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var resolved = await Resolver().ResolveForCurrencyAsync(user, _eur, CancellationToken.None);

        Assert.Equal("cus_new", resolved);
        var row = Assert.Single(_added);
        Assert.Equal((UserId, _eur.Id, "cus_new"), (row.UserId, row.CurrencyId, row.StripeCustomerId));
        Assert.Equal("cus_legacy", user.StripeCustomerId);
    }

    /// <summary>
    /// A row already claims the legacy Customer for a currency (an adoption whose checkout was then
    /// abandoned, so no membership names it): it cannot be adopted twice — the unique index would
    /// refuse — so this currency gets its own Customer.
    /// </summary>
    [Fact]
    public async Task A_Legacy_Customer_Already_Claimed_By_A_Row_Is_Not_Adopted_Again()
    {
        var user = NewUser("cus_legacy");
        _customers
            .Setup(r => r.IsStripeCustomerIdClaimedAsync("cus_legacy", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var resolved = await Resolver().ResolveForCurrencyAsync(user, _eur, CancellationToken.None);

        Assert.Equal("cus_new", resolved);
        _memberships.Verify(r => r.HasAnyInOtherCurrencyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_User_With_No_Customer_At_All_Gets_One_And_The_Legacy_Field_Is_Written()
    {
        var user = NewUser(legacyStripeCustomerId: null);

        var resolved = await Resolver().ResolveForCurrencyAsync(user, _czk, CancellationToken.None);

        Assert.Equal("cus_new", resolved);
        Assert.Equal("cus_new", user.StripeCustomerId);
        var row = Assert.Single(_added);
        Assert.Equal(_czk.Id, row.CurrencyId);
    }
}
