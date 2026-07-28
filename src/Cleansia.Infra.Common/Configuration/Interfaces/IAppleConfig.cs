namespace Cleansia.Infra.Common.Configuration.Interfaces;

/// <summary>
/// The Apple audiences THIS host accepts. Every host binds its own instance, and the set it configures is
/// the whole of its isolation: a host that lists an audience accepts any identity token minted for it.
/// The native and web audiences are therefore kept apart on purpose — see <see cref="WebServicesId"/>.
/// When BOTH are empty the audience check is unsatisfiable and token verification fails closed.
/// </summary>
public interface IAppleConfig
{
    /// <summary>
    /// The NATIVE app bundle id (the <c>cz.cleansia.customer</c> App ID) accepted as an audience when
    /// verifying Apple identity tokens from the iOS app. Bound from the <c>Apple:BundleId</c> setting;
    /// the owner supplies the real value (T-0344). Set on the MOBILE hosts only — a cookie-issuing web
    /// host that accepted it would let a captured iOS identity token mint a browser session.
    /// </summary>
    string BundleId { get; set; }

    /// <summary>
    /// The Sign in with Apple SERVICES ID (<c>cz.cleansia.customer.web</c>) accepted as an audience when
    /// verifying identity tokens from the browser flow. Bound from the <c>Apple:WebServicesId</c>
    /// setting. Set on the customer WEB host only, and it must be grouped under the primary App ID in
    /// the Apple portal — an ungrouped Services ID issues a DIFFERENT <c>sub</c> for the same person, so
    /// existing iOS accounts would not resolve on web.
    /// </summary>
    string WebServicesId { get; set; }
}
