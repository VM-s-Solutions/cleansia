using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Users;

/// <summary>
/// The saved-card reads over real Postgres: a customer's cards are the captured, active ones — a capture
/// Stripe has not confirmed and a removed card are not — with their currency; the replacement read keeps
/// to one currency; and the webhook's read finds a row of another company, which the tenant filter hides.
/// </summary>
[Collection("PostgresCollection")]
public class SavedCardRepositoryTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CzkId = "currency-czk-savedcard";
    private const string EurId = "currency-eur-savedcard";
    private const string OwnerEmail = "card-owner@cleansia.test";

    [Fact]
    public async Task A_Customers_Cards_Are_The_Captured_Active_Ones_With_Their_Currency()
    {
        await TestMethod(
            arrange: SeedAsync,
            act: async provider =>
            {
                var repository = provider.GetRequiredService<ISavedCardRepository>();
                var ctx = provider.GetRequiredService<CleansiaDbContext>();
                var ownerId = await ctx.Users.Where(u => u.Email == OwnerEmail).Select(u => u.Id).SingleAsync();

                var all = await repository.GetCapturedForUserAsync(ownerId, CancellationToken.None);
                var czk = await repository.GetCapturedForUserInCurrencyAsync(ownerId, CzkId, CancellationToken.None);
                return (
                    All: all.Select(c => $"{c.StripePaymentMethodId}:{c.Currency!.Code}").OrderBy(c => c, StringComparer.Ordinal).ToList(),
                    Czk: czk.Select(c => c.StripePaymentMethodId).ToList());
            },
            assert: (CleansiaDbContext _, (List<string> All, List<string?> Czk) r) =>
            {
                Assert.Equal(["pm_czk_live:CZK", "pm_eur_live:EUR"], r.All);
                Assert.Equal(["pm_czk_live"], r.Czk);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task The_Webhook_Read_Finds_Another_Companys_Card_That_The_Tenant_Filter_Hides()
    {
        string cardId = "";
        await TestMethod(
            arrange: async ctx =>
            {
                await SeedAsync(ctx);
                var ownerId = await ctx.Users.Where(u => u.Email == OwnerEmail).Select(u => u.Id).SingleAsync();
                var elsewhere = SavedCard.Start(ownerId, CzkId, "cus_card_owner", null, null);
                elsewhere.TenantId = TestTenants.Second;
                cardId = elsewhere.Id;
                ctx.SavedCards.Add(elsewhere);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var repository = provider.GetRequiredService<ISavedCardRepository>();
                return (
                    Filtered: await repository.GetByIdAsync(cardId, CancellationToken.None),
                    Ignoring: await repository.GetByIdIgnoringTenantAsync(cardId, CancellationToken.None));
            },
            assert: (CleansiaDbContext _, (SavedCard? Filtered, SavedCard? Ignoring) r) =>
            {
                Assert.Null(r.Filtered);
                Assert.Equal(TestTenants.Second, r.Ignoring?.TenantId);
                return Task.CompletedTask;
            });
    }

    private static async Task SeedAsync(CleansiaDbContext ctx)
    {
        ctx.Languages.Add(Language.Create("en", "English"));

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.IsActive = true;
        czk.Id = CzkId;
        czk.SetAsDefault(true);
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.IsActive = true;
        eur.Id = EurId;
        ctx.Currencies.AddRange(czk, eur);

        var owner = User.CreateWithPassword(OwnerEmail, "12345678Test!", "Card", "Owner");
        var other = User.CreateWithPassword("card-other@cleansia.test", "12345678Test!", "Other", "Customer");
        ctx.Users.AddRange(owner, other);

        ctx.SavedCards.AddRange(
            Captured(owner.Id, CzkId, "pm_czk_live"),
            Captured(owner.Id, EurId, "pm_eur_live"),
            Captured(other.Id, CzkId, "pm_other_live"),
            SavedCard.Start(owner.Id, CzkId, "cus_card_owner", "203.0.113.1", "Pending phone"));

        var removed = Captured(owner.Id, CzkId, "pm_czk_removed");
        removed.IsActive = false;
        ctx.SavedCards.Add(removed);

        await ctx.CommitAsync(CancellationToken.None);
    }

    private static SavedCard Captured(string userId, string currencyId, string paymentMethodId)
    {
        var card = SavedCard.Start(userId, currencyId, $"cus_{userId}", null, null);
        card.Capture(paymentMethodId, "visa", "4242", 12, 2030);
        return card;
    }
}
