namespace Cleansia.Infra.Common.Configuration.Interfaces;

/// <summary>
/// Direct-APNs config for the iOS Live Activity channel (ADR-0029 D1) — the token-authenticated
/// (<c>.p8</c>/ES256) <c>liveactivity</c> push that sits BESIDE FCM, never through it. The
/// <c>ApnsLiveActivityClient</c> signs its provider JWT from this key material. Bound from the
/// <c>APNS</c> config section (host + Functions app settings; the binder is case-insensitive).
///
/// <para>Ships INERT: <see cref="Enabled"/> defaults to <c>false</c>, so the already-provisioned key
/// can sit in config with the channel off until the iOS lane (LA-5) ships. While disabled — or with
/// empty key material — the client never opens a socket and the dispatch consumer acks (Skipped), the
/// exact no-op semantics <c>IFcmConfig</c>/<c>FcmPushDispatcher</c> implement for an unconfigured FCM.</para>
/// </summary>
public interface IApnsConfig
{
    /// <summary>
    /// Master switch. <c>false</c> (default) → the client reports Skipped and the consumer acks; the
    /// client never opens a socket. Kept distinct from "empty key material" so the provisioned key can
    /// sit bound-but-off until the iOS lane ships (ADR-0029 RV-3).
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>The 10-char Apple Key ID of the <c>.p8</c> auth key (the JWT <c>kid</c>).</summary>
    string KeyId { get; set; }

    /// <summary>The Apple Developer Team ID (the JWT <c>iss</c>).</summary>
    string TeamId { get; set; }

    /// <summary>
    /// The <c>.p8</c> contents — raw PEM or base64-wrapped PEM (the same dual-accept convention as
    /// <c>FCM:ServiceAccountJson</c>). The same key material already in backend custody for ordinary
    /// push; a config binding, not a new secret. Missing/empty while <see cref="Enabled"/> is true →
    /// treated as Skipped + one startup warning, never a crash.
    /// </summary>
    string PrivateKeyPem { get; set; }

    /// <summary>
    /// The customer app bundle id (<c>cz.cleansia.customer</c>). The <c>apns-topic</c> is DERIVED as
    /// <c>{CustomerBundleId}.push-type.liveactivity</c> — never hardcoded.
    /// </summary>
    string CustomerBundleId { get; set; }

    /// <summary>
    /// <c>true</c> → <c>api.sandbox.push.apple.com</c> (dev builds get sandbox activity tokens);
    /// <c>false</c> → <c>api.push.apple.com</c>. Default true in dev, false in prod.
    /// </summary>
    bool UseSandbox { get; set; }
}
