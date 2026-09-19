using System.Net;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// A validation reject on a paged read. <c>AdminGdprController.GetAllGdprRequests</c> answers
/// <c>PagedData&lt;T&gt;</c> straight from the mediator, so its validator's reject cannot come back as a
/// result — it is thrown and caught on the controller base. Its <c>Limit ≤ 100</c> rule was dead until
/// the validation pipeline ran for every response type; these pin that it runs, that the boundary is
/// admitted, and that the 400 on the wire is the one the returning arm produces (the admin export's
/// unknown-subject reject on the same controller): same title, type, detail, status, content type and
/// member order — a client reads one shape whichever arm the reject took.
/// </summary>
public sealed class ValidationRejectWireContractTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string RequestsRoute = "/api/v1/AdminGdpr/requests";
    private const string ExportRoute = "/api/v1/AdminGdpr/export/nobody-here";

    private HttpClient Admin() =>
        AdminClient(TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local", UserProfile.Administrator));

    [Fact]
    public async Task A_limit_past_the_lists_cap_is_400_page_size_exceeded_keyed_on_the_field()
    {
        var resp = await Admin().GetAsync(RequestsRoute + "?limit=1000");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        await HttpAssert.AssertBusinessErrorAsync(resp, BusinessErrorMessage.PageSizeExceeded);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("Validation Error", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal(BusinessErrorMessage.PageSizeExceeded, doc.RootElement.GetProperty("errors").GetProperty("Limit").GetString());
    }

    [Fact]
    public async Task The_cap_itself_is_admitted()
    {
        var resp = await Admin().GetAsync(RequestsRoute + "?limit=100");

        HttpAssert.IsOk(resp);
    }

    [Fact]
    public async Task The_thrown_reject_wears_the_returning_rejects_shape()
    {
        var client = Admin();

        var thrown = await client.GetAsync(RequestsRoute + "?limit=1000");
        var returned = await client.PostAsync(ExportRoute, content: null);

        Assert.Equal(HttpStatusCode.BadRequest, thrown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, returned.StatusCode);
        Assert.Equal(returned.Content.Headers.ContentType?.ToString(), thrown.Content.Headers.ContentType?.ToString());

        using var thrownDoc = JsonDocument.Parse(await thrown.Content.ReadAsStringAsync());
        using var returnedDoc = JsonDocument.Parse(await returned.Content.ReadAsStringAsync());

        Assert.Equal(Members(returnedDoc), Members(thrownDoc));
        foreach (var member in new[] { "title", "type", "detail", "status" })
        {
            Assert.Equal(returnedDoc.RootElement.GetProperty(member).GetRawText(), thrownDoc.RootElement.GetProperty(member).GetRawText());
        }

        Assert.Equal(BusinessErrorMessage.NotExistingUserWithId, Assert.Single(Errors(returnedDoc)).Value);
        Assert.Equal(BusinessErrorMessage.PageSizeExceeded, Assert.Single(Errors(thrownDoc)).Value);
    }

    private static string[] Members(JsonDocument doc) =>
        doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

    private static IEnumerable<KeyValuePair<string, string>> Errors(JsonDocument doc) =>
        doc.RootElement.GetProperty("errors").EnumerateObject().Select(p => new KeyValuePair<string, string>(p.Name, p.Value.GetString()!));
}
