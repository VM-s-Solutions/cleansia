import Foundation

/// The bundle CleansiaCore's own user-facing strings resolve from. Defaults to
/// `.module`; each app's preferences model calls `apply(languageTag:)` with the
/// `AppSettingsStore`-resolved tag so the in-app language switch reaches Core
/// strings without a restart — the Core mirror of the app-target `L10n.bundle`
/// repointing (T-0310 Slice C).
public enum CoreL10n {
    nonisolated(unsafe) static var bundle: Bundle = .module

    /// The language the READER chose, as a bare tag — the single source of truth for resolving
    /// SERVER-supplied translations.
    ///
    /// **`Locale.current` is not that, and never becomes it.** The in-app language switch writes a
    /// private preference and repoints the bundles; it does not set `AppleLanguages`, so the process
    /// locale keeps reporting the DEVICE language for the lifetime of the app. The app's own strings
    /// are unaffected because they resolve through the bundles above — but anything that picks a
    /// translation out of an API payload by locale silently reads the wrong language. That is why a
    /// partner running the app in Czech on an English phone saw English country names in an otherwise
    /// Czech screen.
    ///
    /// Defaults to `en` to match `bundle`'s own default; both are replaced by `apply` at launch,
    /// before any screen renders.
    nonisolated(unsafe) public private(set) static var languageTag: String = "en"

    public static func apply(languageTag: String) {
        bundle = localizedBundle(for: languageTag)
        self.languageTag = languageTag
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
