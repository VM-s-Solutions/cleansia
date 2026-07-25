import Foundation

/// The public web app both mobile apps link out to. A move off `.cz` (a `.eu`
/// domain is under consideration) must be a one-line edit here — never a grep
/// across two apps and five locale catalogs, so no other Swift file, string
/// catalog or plist may spell the domain out.
public enum CleansiaWeb {
    public static let domain = "cleansia.cz"

    public static let origin = "https://\(domain)"

    public static let supportEmail = "support@\(domain)"

    /// Routed by the customer web app (`app.routes.ts`).
    public static var termsURL: URL {
        url("/terms")
    }

    public static var privacyURL: URL {
        url("/privacy")
    }

    public static func referralLink(code: String) -> String {
        "\(origin)/r/\(code)"
    }

    private static func url(_ path: String) -> URL {
        guard let url = URL(string: origin + path) else {
            fatalError("CleansiaWeb.origin is malformed: \(origin)")
        }
        return url
    }
}
