using System.Text.Json;
using Cleansia.Core.AppServices.Features.Credit.Admin;
using Cleansia.Core.AppServices.Features.Loyalty.Admin;
using Cleansia.Core.Domain.Credit;
using Microsoft.AspNetCore.Http;

namespace Cleansia.Tests.Logging;

/// <summary>
/// S6 — the admin money-out endpoints carry FREE TEXT an operator writes ABOUT A NAMED CUSTOMER, and
/// no field-name denylist can reach it.
///
/// <para><c>IssueCustomerCredit.Command.Note</c> is required and is the only record of why a person
/// decided to compensate someone: "second clean in a row went wrong, cat let out, customer distressed".
/// <c>GrantPointsManually.Command.Reason</c> and its revoke twin are the same shape. These are short
/// bodies with no base64 payload to hide behind, so every one of them was landing in the request log
/// in full — the leak the document-upload paths only had by accident.</para>
///
/// <para>The instrument is <c>IsSensitivePath</c>, the wholesale suppression already used for
/// <c>/auth/</c>, <c>/login</c> and the upload paths — NOT a wider field regex, which would redact
/// every <c>note</c> on the platform including harmless ones. The audit trail keeps these strings
/// under the acting admin's name, which is where they belong; the request log does not need a second
/// plaintext copy.</para>
/// </summary>
public class RequestLogAdminFreeTextPathTests
{
    private const string Suppressed = "[suppressed: sensitive endpoint]";

    public static TheoryData<Type> HostMiddlewareTypes() => RequestLoggingHarness.HostMiddlewareTypes();

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task IssueCredit_TheAdminsNoteAboutTheCustomer_IsSuppressed(Type middlewareType)
    {
        const string marker = "let the cat out and would not answer the door";
        var json = JsonSerializer.Serialize(new IssueCustomerCredit.Command(
            UserId: "01USERCREDIT00000000000001",
            Amount: 500m,
            Reason: CreditTransactionReason.Goodwill,
            Note: marker,
            RequestId: "req-1"));

        AssertWouldOtherwiseBeVisible(middlewareType, json, marker);

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/AdminCredit/issue", responseJson: "{}", requestJson: json,
            method: HttpMethods.Post);

        Assert.NotEmpty(logged);
        Assert.All(logged, message => Assert.DoesNotContain(marker, message));
        Assert.Contains(logged, message => message.Contains(Suppressed));
    }

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task GrantPoints_TheAdminsReason_IsSuppressed(Type middlewareType)
    {
        const string marker = "complained on the phone about the cleaner smoking";
        var json = JsonSerializer.Serialize(new GrantPointsManually.Command(
            UserId: "01USERCREDIT00000000000001",
            Points: 500,
            Reason: marker,
            RequestId: "req-1"));

        AssertWouldOtherwiseBeVisible(middlewareType, json, marker);

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/AdminLoyalty/grant-points", responseJson: "{}", requestJson: json,
            method: HttpMethods.Post);

        Assert.NotEmpty(logged);
        Assert.All(logged, message => Assert.DoesNotContain(marker, message));
        Assert.Contains(logged, message => message.Contains(Suppressed));
    }

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task RevokePoints_TheAdminsReason_IsSuppressed(Type middlewareType)
    {
        const string marker = "gamed the referral scheme with two of his own addresses";
        var json = JsonSerializer.Serialize(new RevokePointsManually.Command(
            UserId: "01USERCREDIT00000000000001",
            Points: 500,
            Reason: marker,
            RequestId: "req-1"));

        AssertWouldOtherwiseBeVisible(middlewareType, json, marker);

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/AdminLoyalty/revoke-points", responseJson: "{}", requestJson: json,
            method: HttpMethods.Post);

        Assert.NotEmpty(logged);
        Assert.All(logged, message => Assert.DoesNotContain(marker, message));
        Assert.Contains(logged, message => message.Contains(Suppressed));
    }

    /// <summary>
    /// Proves the suppression is doing the work. These bodies are tiny and carry no redactable field,
    /// so without the path entry the marker sits well inside the log window and would be printed
    /// verbatim — unlike the upload paths, which were accidentally hidden behind a base64 payload until
    /// redact-before-truncate exposed them.
    /// </summary>
    private static void AssertWouldOtherwiseBeVisible(Type middlewareType, string json, string marker)
    {
        var requestLimit = RequestLoggingHarness.LimitOf(middlewareType, "RequestBodyLimit");

        var markerIndex = json.IndexOf(marker, StringComparison.Ordinal);
        Assert.InRange(markerIndex, 0, requestLimit - 1);

        Assert.InRange(json.Length, 0, RequestLoggingHarness.LimitOf(middlewareType, "RedactionScanLimit"));
    }
}
