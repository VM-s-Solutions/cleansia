using System.Net;
using System.Text.Json;

namespace Cleansia.HostTests.Infrastructure;

/// <summary>
/// Assertions over the host's real HTTP responses. Ownership / not-found rejections in this codebase
/// surface as <c>400 Bad Request</c> with a <c>ProblemDetails</c> whose <c>type</c> carries the
/// <c>BusinessErrorMessage</c> constant (and the per-field code in the <c>errors</c> extension) — see
/// <c>CleansiaApiController.CreateProblemDetails</c>. Policy/role denials surface as <c>403 Forbidden</c>
/// with no body. These helpers assert on the <c>BusinessErrorMessage</c> constant / HTTP status, never
/// a hard-coded message string (testing.md anti-pattern).
/// </summary>
public static class HttpAssert
{
    public static void IsForbidden(HttpResponseMessage response) =>
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    public static void IsUnauthorized(HttpResponseMessage response) =>
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

    public static void IsOk(HttpResponseMessage response) =>
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    public static void IsNotFound(HttpResponseMessage response) =>
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

    /// <summary>The request was NOT served the resource — it is either a policy denial (401/403) or a
    /// business not-found/ownership rejection (400 carrying <paramref name="expectedErrorCode"/>).
    /// Never 200. This is the "→ 403/404, never the other user's resource" contract from the ACs,
    /// mapped onto this codebase's actual status conventions.</summary>
    public static async Task RejectedAsync(HttpResponseMessage response, string expectedErrorCode)
    {
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized
            or HttpStatusCode.NotFound)
            return; // policy-layer denial — acceptable rejection

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertBusinessErrorAsync(response, expectedErrorCode);
    }

    /// <summary>Assert a 400 ProblemDetails carries the given <c>BusinessErrorMessage</c> constant in
    /// its <c>type</c> or its <c>errors</c> extension.</summary>
    public static async Task AssertBusinessErrorAsync(HttpResponseMessage response, string expectedErrorCode)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // CleansiaApiController.CreateProblemDetails maps a single business Error as
        //   type   = Error.Code     (often the FIELD name, e.g. "OrderId")
        //   detail = Error.Message  (the BusinessErrorMessage constant, e.g. "order.not_found")
        // and the per-field `errors` dict carries code→message for validation results. Match any of
        // them so the assertion is robust to which slot the constant lands in.
        var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (type == expectedErrorCode)
            return;

        var detail = root.TryGetProperty("detail", out var d) ? d.GetString() : null;
        if (detail == expectedErrorCode)
            return;

        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in errors.EnumerateObject())
            {
                if (prop.Name == expectedErrorCode)
                    return;

                // A FluentValidation result with two parallel RuleFor chains (e.g. TakeOrder/StartOrder,
                // whose OrderId chain and whole-command chain both fail) serializes its messages
                // "; "-joined under a synthetic property name ("AsyncPredicateValidator"). Match the
                // expected BusinessErrorMessage constant as one of those tokens — still asserting on the
                // constant, never a hard-coded prose string (testing.md anti-pattern). A single-error
                // value is just a one-token list, so exact matches keep working.
                if (TokenizeErrorValue(prop.Value).Contains(expectedErrorCode))
                    return;
            }
        }

        Assert.Fail(
            $"Expected business error '{expectedErrorCode}' in the 400 ProblemDetails but got type='{type}', detail='{detail}'. Body: {body}");
    }

    private static IReadOnlyCollection<string> TokenizeErrorValue(JsonElement value)
    {
        var tokens = new List<string>();
        if (value.ValueKind == JsonValueKind.String)
        {
            var s = value.GetString();
            if (s is not null)
                tokens.AddRange(s.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s)
                    tokens.Add(s);
        }
        return tokens;
    }
}
