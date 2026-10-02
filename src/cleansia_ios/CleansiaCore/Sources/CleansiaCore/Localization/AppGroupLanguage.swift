import Foundation

/// The language picked INSIDE the app, shared with the app's extensions, and the one thing the
/// Notification Service Extension does with it.
///
/// A push alert carries loc-keys, and iOS resolves them in the app's system language — the phone's, or
/// the per-app one in Settings — never in the in-app picker's, which only repoints the app's own
/// bundles. Each extension runs in its own process with its own defaults, so the app mirrors an explicit
/// choice into its App Group at launch and on every change, and the extensions read it from there.
/// "System" leaves nothing there: what the app resolves it to is the phone's language when the app last
/// ran, and the phone can change while the app is not running — iOS's own rendering never goes stale.
/// -> /architecture/push-notifications
///
/// Foundation only, and compiled straight into each Notification Service Extension as well as into Core:
/// the extensions do not link Core, whose resource bundle would otherwise be copied into each of them.
public enum AppGroupLanguage {
    public static let customerAppGroup = "group.cz.cleansia.customer"
    public static let partnerAppGroup = "group.cz.cleansia.partner"

    static let languageKey = "inAppLanguageTag"

    /// Nil — the app follows the phone — removes the tag.
    public static func write(_ languageTag: String?, appGroup: String) {
        UserDefaults(suiteName: appGroup)?.set(languageTag, forKey: languageKey)
    }

    /// Nil while the app follows the phone, and until a build that writes it has launched once; the
    /// extensions then leave iOS's own rendering.
    public static func read(appGroup: String) -> String? {
        UserDefaults(suiteName: appGroup)?.string(forKey: languageKey)
    }

    /// An APNs loc-key alert rendered from `bundle`'s `<languageTag>.lproj` — the alert iOS would have
    /// drawn, in the in-app language. Nil on any miss (no tag, no loc-keys, no table, a key or argument
    /// the table cannot fill), and the caller then delivers the content iOS already resolved: a
    /// notification in the phone's language is a degraded result, a raw `push.*` key never is.
    public static func localizedAlert(
        userInfo: [AnyHashable: Any],
        languageTag: String?,
        bundle: Bundle
    ) -> (title: String, body: String)? {
        guard let languageTag,
              let aps = userInfo["aps"] as? [String: Any],
              let alert = aps["alert"] as? [String: Any],
              let titleKey = alert["title-loc-key"] as? String,
              let bodyKey = alert["loc-key"] as? String,
              let path = bundle.path(forResource: languageTag, ofType: "lproj"),
              let table = Bundle(path: path)
        else { return nil }
        let args = (alert["loc-args"] as? [Any] ?? []).map { "\($0)" }
        guard let title = lookUp(titleKey, in: table).flatMap({ filled($0, with: []) }),
              let body = lookUp(bodyKey, in: table).flatMap({ filled($0, with: args) })
        else { return nil }
        return (title, body)
    }

    private static func lookUp(_ key: String, in table: Bundle) -> String? {
        let value = table.localizedString(forKey: key, value: nil, table: nil)
        return value == key || value.isEmpty ? nil : value
    }

    /// Fills `%1$@`-style slots and `%%`, which is all APNs loc-args fill and all the push copy uses.
    /// Done by hand rather than with `String(format:)`, which reads past its arguments when a format
    /// names a slot nobody sent. Any other specifier, or a slot with no argument, is a miss.
    static func filled(_ format: String, with args: [String]) -> String? {
        var result = ""
        var rest = Substring(format)
        while let percent = rest.firstIndex(of: "%") {
            result += rest[..<percent]
            rest = rest[rest.index(after: percent)...]
            if rest.first == "%" {
                result += "%"
                rest = rest.dropFirst()
                continue
            }
            let digits = rest.prefix { $0.isASCII && $0.isNumber }
            guard let slot = Int(digits), slot >= 1, slot <= args.count,
                  rest.dropFirst(digits.count).hasPrefix("$@")
            else { return nil }
            result += args[slot - 1]
            rest = rest.dropFirst(digits.count + 2)
        }
        return result + rest
    }
}
