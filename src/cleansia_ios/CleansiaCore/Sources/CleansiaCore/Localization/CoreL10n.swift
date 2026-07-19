import Foundation

/// The bundle CleansiaCore's own user-facing strings resolve from. Defaults to
/// `.module`; each app's preferences model calls `apply(languageTag:)` with the
/// `AppSettingsStore`-resolved tag so the in-app language switch reaches Core
/// strings without a restart — the Core mirror of the app-target `L10n.bundle`
/// repointing (T-0310 Slice C).
public enum CoreL10n {
    nonisolated(unsafe) static var bundle: Bundle = .module

    public static func apply(languageTag: String) {
        bundle = localizedBundle(for: languageTag)
    }

    static func localized(_ key: String) -> String {
        bundle.localizedString(forKey: key, value: nil, table: nil)
    }

    static func localizedBundle(for tag: String) -> Bundle {
        guard let path = Bundle.module.path(forResource: tag, ofType: "lproj"),
              let lproj = Bundle(path: path)
        else {
            return .module
        }
        return lproj
    }
}
