using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The consent refusals as they actually arrive at a client, through the real route and the real
/// <c>CleansiaApiController.CreateProblemDetails</c>.
///
/// Every client — web <c>firstErrorKey</c>, Android <c>SafeApiCall</c>, iOS
/// <c>ProblemDetails.firstErrorKey</c> — resolves its translation from the first VALUE in the
/// <c>errors</c> bag, and the bag is built as <c>{ Error.Code : Error.Message }</c>. So the whole
/// user-visible outcome turns on which slot the <c>BusinessErrorMessage</c> constant occupies, and
/// asserting the constant is merely "somewhere in the body" (as <see cref="HttpAssert"/> deliberately
/// does, being shape-tolerant) cannot see the difference. These assert the VALUE specifically.
/// </summary>
public sealed class ConsentErrorWireContractTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string GrantRoute = "/api/v1/Gdpr/consents";
    private const string WithdrawRoute = "/api/v1/Gdpr/consents/withdraw";

    private async Task<(string Id, string Email)> ArrangeCustomerAsync(string email)
    {
        string id = "";

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var customer = DomainSeed.Customer(email);
            ctx.Users.Add(customer);
            id = customer.Id;
        });

        return (id, email);
    }

    private HttpClient ClientFor((string Id, string Email) customer) =>
        CustomerClient(TestJwtFactory.Mint(CustomerAudience, customer.Id, customer.Email, UserProfile.Customer));

    private static async Task<string> FirstErrorValueAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = doc.RootElement.GetProperty("errors");

        var first = errors.EnumerateObject().First();
        return first.Value.GetString()!;
    }

    private static async Task<string> FirstErrorKeyAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("errors").EnumerateObject().First().Name;
    }

    [Fact]
    public async Task A_duplicate_grant_puts_the_translation_key_where_clients_read_it()
    {
        var customer = await ArrangeCustomerAsync("consent-duplicate@hosttests.local");
        var client = ClientFor(customer);
        var body = new { consentType = (int)ConsentType.TermsOfService };

        var first = await client.PostAsJsonAsync(GrantRoute, body);
        var second = await client.PostAsJsonAsync(GrantRoute, body);

        HttpAssert.IsOk(first);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(BusinessErrorMessage.ConsentAlreadyGranted, await FirstErrorValueAsync(second));
        Assert.Equal(nameof(ConsentType), await FirstErrorKeyAsync(second));
    }

    [Fact]
    public async Task Withdrawing_a_consent_that_was_never_given_does_the_same()
    {
        var customer = await ArrangeCustomerAsync("consent-withdraw-missing@hosttests.local");

        var response = await ClientFor(customer).PostAsJsonAsync(
            WithdrawRoute, new { consentType = (int)ConsentType.MarketingEmails });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(BusinessErrorMessage.ConsentNotFound, await FirstErrorValueAsync(response));
        Assert.Equal(nameof(ConsentType), await FirstErrorKeyAsync(response));
    }

    /// <summary>
    /// The swap's signature seen from outside: a bag keyed by the dot code resolves to prose, and the
    /// dot code is then reachable only as the key nothing reads.
    /// </summary>
    [Fact]
    public async Task The_dot_key_is_never_the_bag_key()
    {
        var customer = await ArrangeCustomerAsync("consent-bag-shape@hosttests.local");
        var client = ClientFor(customer);
        var body = new { consentType = (int)ConsentType.PrivacyPolicy };

        await client.PostAsJsonAsync(GrantRoute, body);
        var refusal = await client.PostAsJsonAsync(GrantRoute, body);

        Assert.NotEqual(BusinessErrorMessage.ConsentAlreadyGranted, await FirstErrorKeyAsync(refusal));
        Assert.DoesNotContain(".", await FirstErrorKeyAsync(refusal));
    }
}
