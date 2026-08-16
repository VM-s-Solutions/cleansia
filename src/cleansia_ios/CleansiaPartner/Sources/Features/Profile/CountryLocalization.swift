import CleansiaCore
import CleansiaPartnerApi
import Foundation

public extension CountryListItem {
    /// The country's name in the reader's language.
    ///
    /// `CountryListItem.name` is the raw `Countries.Name` column, which is English by definition — the
    /// seed stores every other language under `translations`. Reading `.name` directly, which every
    /// iOS call site did, showed a Czech partner "Czech Republic" in an otherwise fully Czech screen.
    /// The web has always resolved it this way (`country.translations?.[currentLang]?.name` in the
    /// profile facades); this is the mobile side of the same rule.
    ///
    /// The lookup narrows through `UserDefaultsAppSettingsStore.bareLanguageCode` because a device
    /// locale is region-qualified (`"cs-CZ"`) while the translation map is keyed by bare `"cs"` —
    /// matching without that step silently never hits.
    ///
    /// Falls back through name → ISO code → id, so a country seeded without a translation for this
    /// language still renders something a person can read.
    ///
    /// Twin: `CountryListItem.localizedName` (Android, partner-app). Keep the two in step.
    func localizedName(locale: Locale = .current) -> String {
        let translated = UserDefaultsAppSettingsStore.bareLanguageCode(locale.identifier)
            .flatMap { translations?[$0]?.name }
            .flatMap { $0.isBlank ? nil : $0 }

        return translated
            ?? name.flatMap { $0.isBlank ? nil : $0 }
            ?? isoCode.flatMap { $0.isBlank ? nil : $0 }
            ?? id
            ?? ""
    }
}
