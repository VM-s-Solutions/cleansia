using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// Guest booking is a web-only path: the customer mobile host takes an order only from a signed-in
/// customer, and has one create route. A quote stays anonymous, because the booking flow prices before
/// it asks the customer to sign in.
/// </summary>
public sealed class CustomerMobileOrderCreateRequiresSessionTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string CustomerId = "mobile-create-customer";
    private const string CustomerEmail = "mobile-create-customer@hosttests.local";

    private HostTestApplicationFactory<Cleansia.Web.Mobile.Customer.Program> CustomerMobileHost() =>
        new(Db.ConnectionString);

    [Fact]
    public async Task An_anonymous_create_is_refused()
    {
        using var host = CustomerMobileHost();
        using var client = host.CreateClient();

        HttpAssert.IsUnauthorized(await client.PostAsJsonAsync("/api/Order/CreateOrder", new { }));
    }

    [Fact]
    public async Task The_payment_duplicate_of_the_create_route_is_gone()
    {
        using var host = CustomerMobileHost();
        using var client = host.CreateClient();

        HttpAssert.IsNotFound(await client.PostAsJsonAsync("/api/Payment/CreateOrder", new { }));
    }

    [Fact]
    public async Task A_signed_in_customer_clears_the_gate()
    {
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var customer = DomainSeed.Customer(CustomerEmail);
            customer.Id = CustomerId;
            ctx.Users.Add(customer);
        });
        using var host = CustomerMobileHost();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtFactory.Mint(CustomerAudience, CustomerId, CustomerEmail, UserProfile.Customer));

        HttpAssert.ClearedTheGate(await client.PostAsJsonAsync("/api/Order/CreateOrder", new { }));
    }

    [Fact]
    public async Task A_quote_stays_anonymous()
    {
        using var host = CustomerMobileHost();
        using var client = host.CreateClient();

        HttpAssert.ClearedTheGate(await client.PostAsJsonAsync("/api/Order/Quote", new { }));
    }
}
