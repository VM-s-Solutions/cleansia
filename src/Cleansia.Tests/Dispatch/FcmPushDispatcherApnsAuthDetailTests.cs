using Cleansia.Infra.Clients.Fcm;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// Pins the operator-facing wording for an FCM <c>ThirdPartyAuthError</c>.
///
/// This text is load-bearing, not cosmetic. Two entirely different credentials sit on the push path
/// and BOTH surface as an FCM 401: Google refusing our service account, and Apple refusing the APNs
/// auth key that Firebase holds. The previous message named the Google service account
/// unconditionally, which sent a real investigation into Key Vault, GCP IAM and the deploy pipeline
/// for hours while the actual fault was an APNs key scoped to the Sandbox environment against a
/// TestFlight (Production) build.
///
/// The formatter is public and pure precisely so it can be tested: FirebaseAdmin's
/// <c>BatchResponse</c>/<c>SendResponse</c> constructors are internal to the SDK and there is no
/// InternalsVisibleTo, so the per-token loop that calls this cannot be driven from a unit test.
/// </summary>
public class FcmPushDispatcherApnsAuthDetailTests
{
    private static string Detail() =>
        FcmPushDispatcher.ApnsAuthDetail(401, "Request is missing required authentication credential.");

    [Fact]
    public void Names_Apple_As_The_Refusing_Party()
    {
        Assert.Contains("APPLE", Detail(), StringComparison.Ordinal);
        Assert.Contains("APNs auth key", Detail(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Explicitly_Rules_Out_The_Google_Credential_And_Azure()
    {
        // The single most valuable sentence in the message: it stops the operator before they start
        // re-issuing service-account keys and redeploying, none of which can affect this fault.
        var detail = Detail();

        Assert.Contains("NOT the Google service account", detail, StringComparison.Ordinal);
        Assert.Contains("Key Vault", detail, StringComparison.Ordinal);
        Assert.Contains("no redeploy", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Checks_The_Firebase_Slot_First()
    {
        // Order matters, and this order is what the 2026-07-25 outage actually taught. The fault was
        // a Firebase app card holding a Development APNs auth key with the Production slot EMPTY: a
        // TestFlight build sends a Production token, FCM finds no production key, and every push
        // fails — while Xcode-installed builds keep working perfectly, so it reads as "push broke on
        // its own". That asymmetry is invisible unless someone thinks to look at the slots, and the
        // whole investigation went through the Apple key, the Google service account, Key Vault and
        // the deploy pipeline before anyone did.
        var detail = Detail();
        var slotIndex = detail.IndexOf("SLOT", StringComparison.Ordinal);
        var keyIdIndex = detail.IndexOf("Key ID", StringComparison.Ordinal);
        var environmentIndex = detail.IndexOf("ENVIRONMENT", StringComparison.Ordinal);

        Assert.True(slotIndex >= 0, "The development/production SLOT check is not mentioned at all.");
        Assert.True(keyIdIndex >= 0, "The Key ID check is not mentioned at all.");
        Assert.True(environmentIndex >= 0, "The environment scope is not mentioned at all.");
        Assert.True(slotIndex < keyIdIndex && slotIndex < environmentIndex,
            "The Firebase development/production SLOT must be the FIRST thing checked — it is the " +
            "cause that presents identically to a bad key while leaving Xcode builds working.");
    }

    [Fact]
    public void Names_The_Empty_Production_Slot_In_The_Consoles_Own_Words()
    {
        // So an operator can ctrl-F the log text against what the Firebase console literally shows.
        Assert.Contains("No production APNs auth key", Detail(), StringComparison.Ordinal);
    }

    [Fact]
    public void Names_Both_Sandbox_And_Production_So_The_Mismatch_Is_Recognisable()
    {
        var detail = Detail();

        Assert.Contains("Sandbox", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Production", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TestFlight", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ends_With_The_Providers_Own_Words()
    {
        // Whatever our triage text says, FCM's literal message must survive to the log — it is the
        // only part that reflects what actually happened rather than what we predicted.
        Assert.EndsWith("FCM said: Request is missing required authentication credential.", Detail(), StringComparison.Ordinal);
    }

    [Fact]
    public void Degrades_Cleanly_When_The_Provider_Sent_Nothing()
    {
        var detail = FcmPushDispatcher.ApnsAuthDetail(httpStatus: null, providerMessage: null);

        Assert.Contains("HTTP ?", detail, StringComparison.Ordinal);
        Assert.Contains("(no message)", detail, StringComparison.Ordinal);
        Assert.Contains("APPLE", detail, StringComparison.Ordinal);
    }
}
