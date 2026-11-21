using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The confirmed subscribe route with no trial, end to end on the Customer host over a recording Stripe client:
/// a declined first invoice answers the card's reason and leaves no membership and no live subscription; a
/// 3-D Secure step answers the invoice's PaymentIntent secret and no membership, and the same confirm sent again
/// once the invoice is paid grants exactly one.
/// </summary>
public sealed class MembershipSubscribeFirstInvoiceRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string CustomerEmail = "first-invoice@hosttests.local";
    private const string PlanCode = "FIRST-INVOICE-MONTHLY";

    private readonly RecordingStripeClient _stripe = new();

    protected override void ConfigureCustomerHostServices(IServiceCollection services)
    {
        services.RemoveAll<IStripeClient>();
        services.AddSingleton<IStripeClient>(_stripe);
    }

    private async Task<string> SeedAsync()
    {
        var customerId = "";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var plan = DomainSeed.MembershipPlan(PlanCode);
            ctx.MembershipPlans.Add(plan);
            ctx.MembershipPlanPrices.Add(DomainSeed.MembershipPlanPrice(plan.Id, PlanCode, 199m));
            var customer = DomainSeed.Customer(CustomerEmail);
            ctx.Users.Add(customer);
            customerId = customer.Id;
        });
        return customerId;
    }

    private Task<HttpResponseMessage> ConfirmAsync(HttpClient client) =>
        client.PostAsJsonAsync(
            "/api/Membership/Subscribe",
            new { PlanCode, PaymentMethodConfirmed = true, IdempotencyToken = "first-invoice-1" });

    private Task<List<UserMembership>> MembershipsAsync(string customerId) =>
        QueryAsync(ctx => ctx.UserMemberships.IgnoreQueryFilters().Where(m => m.UserId == customerId).ToListAsync());

    [Fact]
    public async Task A_declined_first_invoice_is_refused_with_the_cards_reason_and_leaves_nothing_behind()
    {
        var customerId = await SeedAsync();
        var client = CustomerClient(TestJwtFactory.Mint(CustomerAudience, customerId, CustomerEmail, UserProfile.Customer));
        _stripe.FirstInvoiceOutcomes.Enqueue(("incomplete", null, "insufficient_funds"));

        var response = await ConfirmAsync(client);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        await HttpAssert.AssertBusinessErrorAsync(response, "membership.card_insufficient_funds");
        Assert.Empty(await MembershipsAsync(customerId));
        Assert.Equal("sub_fake_1", Assert.Single(_stripe.CancelledNow));
    }

    [Fact]
    public async Task A_3ds_first_invoice_is_granted_only_by_the_confirm_that_finds_it_paid()
    {
        var customerId = await SeedAsync();
        var client = CustomerClient(TestJwtFactory.Mint(CustomerAudience, customerId, CustomerEmail, UserProfile.Customer));
        _stripe.FirstInvoiceOutcomes.Enqueue(("incomplete", "pi_fake_secret_1", null));

        var challenge = await ConfirmAsync(client);

        HttpAssert.IsOk(challenge);
        var body = JsonDocument.Parse(await challenge.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("pi_fake_secret_1", body.GetProperty("paymentIntentClientSecret").GetString());
        Assert.Equal(string.Empty, body.GetProperty("membershipId").GetString());
        Assert.Empty(await MembershipsAsync(customerId));

        var paid = await ConfirmAsync(client);

        HttpAssert.IsOk(paid);
        var membershipId = JsonDocument.Parse(await paid.Content.ReadAsStringAsync()).RootElement.GetProperty("membershipId").GetString();
        var membership = Assert.Single(await MembershipsAsync(customerId));
        Assert.Equal((membershipId, "sub_fake_1", MembershipStatus.Active), (membership.Id, membership.StripeSubscriptionId, membership.Status));
        Assert.Empty(_stripe.CancelledNow);
    }
}
