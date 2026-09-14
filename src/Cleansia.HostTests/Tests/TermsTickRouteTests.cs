using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Auditing;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The terms gate as a client meets it (owner ruling 2026-09-14): an anonymous registration on the
/// Customer host that omits the tick, or sends it false, is a 400 whose <c>errors</c> bag carries
/// <c>consent.terms_not_accepted</c> as the first VALUE under the <c>TermsAccepted</c> key — the slot
/// every client resolves its translation from — creates no account, and leaves one
/// <c>customer.account.register</c> failure row with that key. The same body with the tick is a 200.
/// </summary>
public sealed class TermsTickRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string Route = "/api/Auth/Register";
    private const string Email = "terms-tick@hosttests.local";
    private const string Password = "12345678Test!";

    private Task SeedReferenceDataAsync() => SeedAsync(DomainSeed.EnsureReferenceDataAsync);

    private static object Registration(bool? termsAccepted) => new
    {
        email = Email,
        password = Password,
        firstName = "Terms",
        lastName = "Tick",
        language = DomainSeed.LanguageCode,
        termsAccepted,
    };

    private static async Task<(string Key, string Value)> FirstErrorAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var first = doc.RootElement.GetProperty("errors").EnumerateObject().First();
        return (first.Name, first.Value.GetString()!);
    }

    private Task<List<CustomerActionAudit>> CustomerRowsAsync() =>
        QueryAsync(ctx => ctx.CustomerActionAudits.IgnoreQueryFilters().ToListAsync());

    private Task<int> AccountsAsync() =>
        QueryAsync(ctx => ctx.Users.IgnoreQueryFilters().CountAsync(u => u.Email == Email));

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task A_Registration_Without_The_Tick_Is_A_400_Carrying_The_Key_Where_Clients_Read_It_And_Creates_No_Account(bool? termsAccepted)
    {
        await SeedReferenceDataAsync();

        var response = await CustomerClientAnonymous().PostAsJsonAsync(Route, Registration(termsAccepted));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var (key, value) = await FirstErrorAsync(response);
        Assert.Equal("TermsAccepted", key);
        Assert.Equal(BusinessErrorMessage.TermsNotAccepted, value);
        Assert.Equal(0, await AccountsAsync());

        var row = Assert.Single(await CustomerRowsAsync());
        Assert.Equal("customer.account.register", row.Action);
        Assert.False(row.Success);
        Assert.Equal(BusinessErrorMessage.TermsNotAccepted, row.ErrorCode);
        Assert.Null(row.UserId);
    }

    [Fact]
    public async Task The_Same_Registration_With_The_Tick_Is_A_200_And_Creates_The_Account()
    {
        await SeedReferenceDataAsync();

        var response = await CustomerClientAnonymous().PostAsJsonAsync(Route, Registration(termsAccepted: true));

        HttpAssert.IsOk(response);
        Assert.Equal(1, await AccountsAsync());
        var row = Assert.Single(await CustomerRowsAsync());
        Assert.True(row.Success);
    }
}
