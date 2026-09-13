using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Currencies;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Memberships;

/// <summary>
/// A Stripe Customer per (user, currency) over real Postgres (owner ruling 2026-09-13, Q-MARKET-05):
/// the two uniques the migration emits, the lookup that finds a user by ANY of their Customer ids,
/// the resolver's adopt-or-create decision against real membership rows, and the currency in-use
/// guard.
/// </summary>
[Collection("PostgresCollection")]
public class UserStripeCustomerTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CzkId = "currency-czk-stripecust";
    private const string EurId = "currency-eur-stripecust";
    private const string PlanId = "plan-monthly-stripecust";
    private const string LegacyCustomerId = "cus_legacy_stripecust";

    [Fact]
    public async Task The_Schema_Refuses_A_Second_Row_Per_User_And_Currency_And_A_Shared_Customer_Id()
    {
        await TestMethod(
            arrange: ctx => SeedAsync(ctx),
            act: async provider =>
            {
                var ctx = provider.GetRequiredService<CleansiaDbContext>();
                var userId = await ctx.Users.Select(u => u.Id).SingleAsync();

                ctx.UserStripeCustomers.Add(UserStripeCustomer.Create(userId, CzkId, "cus_czk_1"));
                await ctx.CommitAsync(CancellationToken.None);

                ctx.UserStripeCustomers.Add(UserStripeCustomer.Create(userId, CzkId, "cus_czk_2"));
                var duplicatePair = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.CommitAsync(CancellationToken.None));
                ctx.ChangeTracker.Clear();

                ctx.UserStripeCustomers.Add(UserStripeCustomer.Create(userId, EurId, "cus_czk_1"));
                var sharedCustomer = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.CommitAsync(CancellationToken.None));
                ctx.ChangeTracker.Clear();

                return (
                    Pair: (duplicatePair.InnerException as PostgresException)?.ConstraintName,
                    Customer: (sharedCustomer.InnerException as PostgresException)?.ConstraintName);
            },
            assert: (CleansiaDbContext _, (string? Pair, string? Customer) r) =>
            {
                Assert.Equal("IX_UserStripeCustomers_UserId_CurrencyId", r.Pair);
                Assert.Equal("IX_UserStripeCustomers_StripeCustomerId", r.Customer);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_User_Is_Found_By_The_Legacy_Customer_Id_And_By_Any_Per_Currency_One()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                await SeedAsync(ctx);
                var userId = await ctx.Users.Select(u => u.Id).SingleAsync();
                ctx.UserStripeCustomers.Add(UserStripeCustomer.Create(userId, EurId, "cus_eur_lookup"));
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var repository = provider.GetRequiredService<IUserStripeCustomerRepository>();
                var ctx = provider.GetRequiredService<CleansiaDbContext>();
                var userId = await ctx.Users.Select(u => u.Id).SingleAsync();
                return (
                    UserId: userId,
                    ByLegacy: await repository.FindUserIdByStripeCustomerIdAsync(LegacyCustomerId, CancellationToken.None),
                    ByRow: await repository.FindUserIdByStripeCustomerIdAsync("cus_eur_lookup", CancellationToken.None),
                    Unknown: await repository.FindUserIdByStripeCustomerIdAsync("cus_nobody", CancellationToken.None));
            },
            assert: (CleansiaDbContext _, (string UserId, string? ByLegacy, string? ByRow, string? Unknown) r) =>
            {
                Assert.Equal(r.UserId, r.ByLegacy);
                Assert.Equal(r.UserId, r.ByRow);
                Assert.Null(r.Unknown);
                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// The ruling's own case: a cancelled CZK membership on the legacy Customer, now subscribing in
    /// EUR — a second Customer is created and recorded; CZK itself adopts the legacy one.
    /// </summary>
    [Fact]
    public async Task After_A_Cancelled_Czk_Membership_Eur_Gets_A_New_Customer_And_Czk_Adopts_The_Legacy_One()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                await SeedAsync(ctx);
                var userId = await ctx.Users.Select(u => u.Id).SingleAsync();
                var cancelled = UserMembership.Create(userId, PlanId, CzkId, "sub_czk_cancelled", DateTime.UtcNow.AddMonths(-2), DateTime.UtcNow.AddMonths(-1));
                cancelled.UpdateFromStripeWebhook("canceled", cancelled.CurrentPeriodStart, cancelled.CurrentPeriodEnd, null);
                ctx.UserMemberships.Add(cancelled);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var ctx = provider.GetRequiredService<CleansiaDbContext>();
                var user = await ctx.Users.SingleAsync();
                var eur = await ctx.Currencies.SingleAsync(c => c.Id == EurId);
                var czk = await ctx.Currencies.SingleAsync(c => c.Id == CzkId);

                var stripe = new Mock<IStripeClient>();
                stripe
                    .Setup(c => c.CreateCustomerAsync(user.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("cus_eur_new");
                var resolver = new StripeCustomerResolver(
                    provider.GetRequiredService<IUserStripeCustomerRepository>(),
                    provider.GetRequiredService<IUserMembershipRepository>(),
                    stripe.Object,
                    NullLogger<StripeCustomerResolver>.Instance);

                var forEur = await resolver.ResolveForCurrencyAsync(user, eur, CancellationToken.None);
                await ctx.CommitAsync(CancellationToken.None);
                var forCzk = await resolver.ResolveForCurrencyAsync(user, czk, CancellationToken.None);
                await ctx.CommitAsync(CancellationToken.None);
                var forEurAgain = await resolver.ResolveForCurrencyAsync(user, eur, CancellationToken.None);

                stripe.Verify(c => c.CreateCustomerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
                return (forEur, forCzk, forEurAgain, user.StripeCustomerId);
            },
            assert: async (CleansiaDbContext ctx, (string ForEur, string ForCzk, string ForEurAgain, string? Legacy) r) =>
            {
                Assert.Equal("cus_eur_new", r.ForEur);
                Assert.Equal(LegacyCustomerId, r.ForCzk);
                Assert.Equal("cus_eur_new", r.ForEurAgain);
                Assert.Equal(LegacyCustomerId, r.Legacy);

                var rows = await ctx.UserStripeCustomers.IgnoreQueryFilters()
                    .OrderBy(c => c.CurrencyId)
                    .Select(c => new { c.CurrencyId, c.StripeCustomerId })
                    .ToListAsync();
                Assert.Equal([(CzkId, LegacyCustomerId), (EurId, "cus_eur_new")], rows.Select(c => (c.CurrencyId, c.StripeCustomerId)));
            });
    }

    [Fact]
    public async Task A_Currency_With_A_Stripe_Customer_Opened_For_It_Answers_In_Use()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                await SeedAsync(ctx);
                var userId = await ctx.Users.Select(u => u.Id).SingleAsync();
                ctx.UserStripeCustomers.Add(UserStripeCustomer.Create(userId, EurId, "cus_eur_inuse"));
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new DeleteCurrency.Command(EurId)),
            assert: async (CleansiaDbContext ctx, BusinessResult<DeleteCurrency.Response> r) =>
            {
                Assert.True(r.IsFailure);
                Assert.Contains(Assert.IsAssignableFrom<IValidationResult>(r).Errors, e => e.Message == BusinessErrorMessage.CurrencyInUse);
                Assert.True(await ctx.Currencies.AnyAsync(c => c.Id == EurId));
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

        var plan = MembershipPlan.Create("PLUS_MONTHLY", "Cleansia Plus (Monthly)", 5m, 4, true);
        plan.Id = PlanId;
        ctx.MembershipPlans.Add(plan);

        var user = User.CreateWithPassword("stripecust@cleansia.test", "12345678Test!", "Stripe", "Customer");
        user.ConfirmEmail();
        user.AssignStripeCustomerId(LegacyCustomerId);
        ctx.Users.Add(user);

        await ctx.CommitAsync(CancellationToken.None);
    }
}
