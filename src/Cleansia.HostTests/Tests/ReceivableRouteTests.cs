using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.HostTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// What a customer owes, on the Customer host with a recording Stripe client and the default configuration,
/// where off-session charging is switched off (owner ruling 2026-09-28, decisions 16 and 18): a customer reads
/// their own open receivables across companies and opens a pay link for one; another customer's receivable
/// and one written off are refused; an anonymous caller is refused.
/// </summary>
public sealed class ReceivableRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string OwnerEmail = "receivable-owner@hosttests.local";
    private const string OutsiderEmail = "receivable-outsider@hosttests.local";

    private readonly RecordingStripeClient _stripe = new();

    protected override void ConfigureCustomerHostServices(IServiceCollection services)
    {
        services.RemoveAll<IStripeClient>();
        services.AddSingleton<IStripeClient>(_stripe);
    }

    private sealed record Seeded(string OwnerId, string OwnId, string OtherCompanysId, string WrittenOffId, string OutsidersId);

    private async Task<Seeded> ArrangeAsync()
    {
        Seeded seeded = null!;
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var owner = DomainSeed.Customer(OwnerEmail);
            var outsider = DomainSeed.Customer(OutsiderEmail);
            ctx.Users.AddRange(owner, outsider);

            var own = OwedOn(DomainSeed.NewOrder(owner.Id, owner.Email), ctx, 375m);
            var otherCompanys = OwedOn(DomainSeed.NewOrder(owner.Id, owner.Email, HostTestTenants.B), ctx, 150m);
            otherCompanys.TenantId = HostTestTenants.B;
            var writtenOff = OwedOn(DomainSeed.NewOrder(owner.Id, owner.Email), ctx, 90m);
            writtenOff.WriteOff("admin-1", "Goodwill", DateTimeOffset.UtcNow.AddDays(-1));
            var outsiders = OwedOn(DomainSeed.NewOrder(outsider.Id, outsider.Email), ctx, 60m);

            seeded = new Seeded(owner.Id, own.Id, otherCompanys.Id, writtenOff.Id, outsiders.Id);
        });
        return seeded;
    }

    private static Receivable OwedOn(Core.Domain.Orders.Order order, Infra.Database.CleansiaDbContext ctx, decimal amount)
    {
        var receivable = Receivable.ForCashCancellationFee(order, amount);
        ctx.Orders.Add(order);
        ctx.Receivables.Add(receivable);
        return receivable;
    }

    private HttpClient Owner(Seeded s) =>
        CustomerClient(TestJwtFactory.Mint(CustomerAudience, s.OwnerId, OwnerEmail, UserProfile.Customer));

    [Fact]
    public async Task A_Customer_Reads_Their_Own_Open_Receivables_Across_Companies()
    {
        var s = await ArrangeAsync();

        var response = await Owner(s).GetAsync("/api/Receivable/GetMine");

        HttpAssert.IsOk(response);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var ids = body.EnumerateArray().Select(r => r.GetProperty("id").GetString()).ToHashSet();
        Assert.Equal(new HashSet<string?> { s.OwnId, s.OtherCompanysId }, ids);
        var own = Assert.Single(body.EnumerateArray(), r => r.GetProperty("id").GetString() == s.OwnId);
        Assert.Equal(375m, own.GetProperty("amount").GetDecimal());
        Assert.Equal("CZK", own.GetProperty("currencyCode").GetString());
    }

    [Fact]
    public async Task A_Customer_Opens_A_Pay_Link_While_Off_Session_Charging_Is_Switched_Off()
    {
        var s = await ArrangeAsync();

        var response = await Owner(s).PostAsync($"/api/Receivable/CreatePayLink/{s.OwnId}", content: null);

        HttpAssert.IsOk(response);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal($"https://checkout.stripe.test/pay/{s.OwnId}", body.GetProperty("checkoutUrl").GetString());
        Assert.Equal((s.OwnId, 375m, "CZK"), Assert.Single(_stripe.ReceivableCheckouts));
    }

    [Fact]
    public async Task Another_Customers_Receivable_Is_Not_Found()
    {
        var s = await ArrangeAsync();

        var response = await Owner(s).PostAsync($"/api/Receivable/CreatePayLink/{s.OutsidersId}", content: null);

        await HttpAssert.RejectedAsync(response, BusinessErrorMessage.ReceivableNotFound);
        Assert.Empty(_stripe.ReceivableCheckouts);
    }

    [Fact]
    public async Task A_Receivable_Written_Off_Is_Not_Payable()
    {
        var s = await ArrangeAsync();

        var response = await Owner(s).PostAsync($"/api/Receivable/CreatePayLink/{s.WrittenOffId}", content: null);

        await HttpAssert.RejectedAsync(response, BusinessErrorMessage.ReceivableNotOpen);
        Assert.Empty(_stripe.ReceivableCheckouts);
    }

    [Fact]
    public async Task An_Anonymous_Caller_Is_Refused()
    {
        HttpAssert.IsUnauthorized(await CustomerClientAnonymous().GetAsync("/api/Receivable/GetMine"));
        HttpAssert.IsUnauthorized(await CustomerClientAnonymous().PostAsync("/api/Receivable/CreatePayLink/r-1", content: null));
    }
}
