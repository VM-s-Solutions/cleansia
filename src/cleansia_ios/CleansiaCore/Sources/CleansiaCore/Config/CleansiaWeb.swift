import Foundation

/// The public web app both mobile apps link out to. A move off `.cz` (a `.eu`
/// domain is under consideration) must be a one-line edit here — never a grep
/// across two apps and five locale catalogs, so no other Swift file, string
/// catalog or plist may spell the domain out.
public enum CleansiaWeb {
    public static let domain = "cleansia.cz"

    public static let origin = "https://\(domain)"

    /// The one support contact customers and cleaners are shown or linked to (owner ruling 2026-10-02).
    public static let contactEmail = "support@\(domain)"

    /// Routed by the customer web app (`app.routes.ts`).
    public static var termsURL: URL {
        url("/terms")
    }

    public static var privacyURL: URL {
        url("/privacy")
    }

    public static let partnerOrigin = "https://partner.\(domain)"

    /// Routed by the partner web app (`app.routes.ts`, `HOW_JOBS_ARE_OFFERED_PATH`).
    public static var howJobsAreOfferedURL: URL {
        url("/how-jobs-are-offered", base: partnerOrigin)
    }

    public static func referralLink(code: String) -> String {
        "\(origin)/r/\(code)"
    }

    private static func url(_ path: String, base: String = CleansiaWeb.origin) -> URL {
        guard let url = URL(string: base + path) else {
            fatalError("CleansiaWeb origin is malformed: \(base)")
        }
        return url
    }
}
