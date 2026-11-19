using System.Text.Json;
using Cleansia.HostTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// Every host's own MVC serializer writes timestamps at the microseconds Postgres stores. Deterministic,
/// unlike a route test: a macOS clock already ticks in whole microseconds, a Linux one in 100 ns.
/// </summary>
public sealed class ApiTimestampPrecisionTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    [Theory]
    [InlineData("Partner")]
    [InlineData("Admin")]
    [InlineData("Customer")]
    [InlineData("PartnerMobile")]
    [InlineData("CustomerMobile")]
    public void Every_Host_Writes_Timestamps_At_Stored_Precision(string host)
    {
        using var customerMobile = host == "CustomerMobile"
            ? new HostTestApplicationFactory<Cleansia.Web.Mobile.Customer.Program>(Db.ConnectionString)
            : null;
        var services = host switch
        {
            "Partner" => PartnerHost.Services,
            "Admin" => AdminHost.Services,
            "Customer" => CustomerHost.Services,
            "PartnerMobile" => MobileHost.Services,
            _ => customerMobile!.Services,
        };
        var options = services.GetRequiredService<IOptions<MvcJsonOptions>>().Value.JsonSerializerOptions;
        var at = new DateTime(2026, 11, 18, 13, 32, 51, DateTimeKind.Utc).AddTicks(1991616);

        Assert.Equal("{\"at\":\"2026-11-18T13:32:51.199161Z\",\"atOffset\":\"2026-11-18T13:32:51.199161+00:00\"}",
            JsonSerializer.Serialize(new { At = at, AtOffset = new DateTimeOffset(at) }, options));
    }
}
