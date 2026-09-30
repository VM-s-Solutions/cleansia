using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The saved card on the Customer host, with a recording Stripe client (owner ruling 2026-09-28, decision
/// 16): starting a web capture records the consent on a pending card and returns a setup-mode checkout on
/// the customer's Stripe Customer; without the consent nothing starts; a customer reads only their own
/// captured cards, removes their own, and cannot remove another customer's.
/// </summary>
public sealed class SavedCardRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string OwnerEmail = "card-owner@hosttests.local";
    private const string OutsiderEmail = "card-outsider@hosttests.local";

    private readonly RecordingStripeClient _stripe = new();

    protected override void ConfigureCustomerHostServices(IServiceCollection services)
    {
        services.RemoveAll<IStripeClient>();
        services.AddSingleton<IStripeClient>(_stripe);
    }

    private sealed record Seeded(string OwnerId, string OutsiderId, string OwnerCardId, string OutsiderCardId);

    private async Task<Seeded> ArrangeAsync(bool withCards = true)
    {
        Seeded seeded = null!;
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var owner = DomainSeed.Customer(OwnerEmail);
            var outsider = DomainSeed.Customer(OutsiderEmail);
            ctx.Users.AddRange(owner, outsider);

            var ownerCard = Captured(owner.Id, "pm_owner");
            var outsiderCard = Captured(outsider.Id, "pm_outsider");
            if (withCards)
            {
                ctx.SavedCards.AddRange(
                    ownerCard,
                    outsiderCard,
                    SavedCard.Start(owner.Id, DomainSeed.CurrencyId, "cus_owner", null, null));
            }

            seeded = new Seeded(owner.Id, outsider.Id, ownerCard.Id, outsiderCard.Id);
        });
        return seeded;
    }

    private static SavedCard Captured(string userId, string paymentMethodId)
    {
        var card = SavedCard.Start(userId, DomainSeed.CurrencyId, $"cus_{paymentMethodId}", null, null);
        card.Capture(paymentMethodId, "visa", "4242", 12, 2030);
        return card;
    }

    private HttpClient Owner(Seeded s) =>
        CustomerClient(TestJwtFactory.Mint(CustomerAudience, s.OwnerId, OwnerEmail, UserProfile.Customer));

    [Fact]
    public async Task Starting_a_web_capture_records_the_consent_on_a_pending_card_and_returns_a_setup_checkout()
    {
        var s = await ArrangeAsync(withCards: false);

        var response = await Owner(s).PostAsJsonAsync("/api/SavedCard/CreateCheckoutSession", new { ConsentAccepted = true });

        HttpAssert.IsOk(response);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var cardId = body.GetProperty("savedCardId").GetString()!;
        Assert.Equal($"https://checkout.stripe.test/setup/{cardId}", body.GetProperty("checkoutUrl").GetString());
        Assert.Equal(("cus_fake_1", cardId), Assert.Single(_stripe.CardSetupCheckouts));

        var card = await QueryAsync(ctx => ctx.SavedCards.IgnoreQueryFilters().SingleAsync(c => c.Id == cardId));
        Assert.Equal((s.OwnerId, DomainSeed.CurrencyId, "cus_fake_1"), (card.UserId, card.CurrencyId, card.StripeCustomerId));
        Assert.Equal(SavedCard.ConsentTextVersionInForce, card.ConsentTextVersion);
        Assert.False(card.IsCaptured);
        Assert.True(card.IsActive);
    }

    [Fact]
    public async Task Without_the_consent_no_capture_starts()
    {
        var s = await ArrangeAsync(withCards: false);

        var response = await Owner(s).PostAsJsonAsync("/api/SavedCard/CreateCheckoutSession", new { ConsentAccepted = false });

        await HttpAssert.RejectedAsync(response, BusinessErrorMessage.SavedCardConsentNotAccepted);
        Assert.Empty(_stripe.CardSetupCheckouts);
        Assert.False(await QueryAsync(ctx => ctx.SavedCards.IgnoreQueryFilters().AnyAsync()));
    }

    [Fact]
    public async Task A_customer_reads_only_their_own_captured_card()
    {
        var s = await ArrangeAsync();

        var response = await Owner(s).GetAsync("/api/SavedCard/GetMine");

        HttpAssert.IsOk(response);
        var cards = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var card = Assert.Single(cards.EnumerateArray());
        Assert.Equal(s.OwnerCardId, card.GetProperty("id").GetString());
        Assert.Equal("4242", card.GetProperty("last4").GetString());
        Assert.Equal("CZK", card.GetProperty("currencyCode").GetString());
    }

    [Fact]
    public async Task Another_customers_card_cannot_be_removed_and_stays_active()
    {
        var s = await ArrangeAsync();

        var response = await Owner(s).DeleteAsync($"/api/SavedCard/Remove/{s.OutsiderCardId}");

        await HttpAssert.RejectedAsync(response, BusinessErrorMessage.SavedCardNotFound);
        Assert.True(await QueryAsync(ctx => ctx.SavedCards.IgnoreQueryFilters().AnyAsync(c => c.Id == s.OutsiderCardId && c.IsActive)));
    }

    [Fact]
    public async Task A_customer_removes_their_own_card()
    {
        var s = await ArrangeAsync();

        var response = await Owner(s).DeleteAsync($"/api/SavedCard/Remove/{s.OwnerCardId}");

        HttpAssert.IsOk(response);
        Assert.False(await QueryAsync(ctx => ctx.SavedCards.IgnoreQueryFilters().AnyAsync(c => c.Id == s.OwnerCardId && c.IsActive)));
    }

    [Fact]
    public async Task An_anonymous_caller_is_refused()
    {
        var response = await CustomerClientAnonymous().GetAsync("/api/SavedCard/GetMine");

        HttpAssert.IsUnauthorized(response);
    }
}
