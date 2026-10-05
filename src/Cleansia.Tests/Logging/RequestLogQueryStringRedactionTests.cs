namespace Cleansia.Tests.Logging;

/// <summary>
/// The request line prints the query string on every host, and a query value is whatever the caller typed or
/// holds: a guest's access token, an address, home coordinates, an admin's search for a person. Names stay so
/// the line still says what was asked; every value is masked.
/// </summary>
public class RequestLogQueryStringRedactionTests
{
    public static TheoryData<Type, string, string, string, string[]> Cases()
    {
        var data = new TheoryData<Type, string, string, string, string[]>();
        foreach (var middleware in RequestLoggingHarness.AllHostMiddleware)
        {
            data.Add(middleware, "/api/Order/Lookup", "?token=gst-SECRET-123",
                "?token=***REDACTED***", ["gst-SECRET-123"]);
            data.Add(middleware, "/api/AddressSearch/search", "?q=Vinohradsk%C3%A1%2012%20Praha&country=CZ&limit=5",
                "?q=***REDACTED***&country=***REDACTED***&limit=***REDACTED***",
                ["Vinohradsk", "Vinohradská 12 Praha", "country=CZ", "limit=5"]);
            data.Add(middleware, "/api/AddressSearch/map", "?lat=50.0812345&lng=14.4212345",
                "?lat=***REDACTED***&lng=***REDACTED***", ["50.0812345", "14.4212345"]);
            data.Add(middleware, "/api/v1/AdminCustomer", "?Filter.SearchTerm=jan.novak%40example.test&Offset=0",
                "?Filter.SearchTerm=***REDACTED***&Offset=***REDACTED***",
                ["jan.novak", "jan.novak@example.test", "Offset=0"]);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_request_line_keeps_every_query_name_and_masks_every_value(
        Type middleware, string path, string queryString, string expectedQuery, string[] secrets)
    {
        var logs = await RequestLoggingHarness.RunAsync(middleware, path, responseJson: "{}", queryString: queryString);

        var requestLine = Assert.Single(logs, line => line.Contains($" GET {path}", StringComparison.Ordinal)
                                                       && line.Contains("| User:", StringComparison.Ordinal)
                                                       && line.Contains("| IP:", StringComparison.Ordinal));
        Assert.Contains($"GET {path}{expectedQuery} |", requestLine, StringComparison.Ordinal);

        var text = string.Join("\n", logs);
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
        }
    }
}
