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
    /// **Keyed on the language the reader CHOSE, not on the device's.** This defaulted to
    /// `Locale.current`, and the in-app language switch never reaches that: it repoints the string
    /// bundles but does not set `AppleLanguages`, so the process locale reports the device language
    /// forever. A partner running the app in Czech on an English phone got "Czech Republic" in an
    /// otherwise Czech screen — the exact bug this method was written to fix, reintroduced one layer
    /// down by trusting the wrong source. `CoreL10n.languageTag` is what the switch actually sets.
    ///
    /// The lookup still narrows through `UserDefaultsAppSettingsStore.bareLanguageCode` because a tag
    /// may be region-qualified (`"cs-CZ"`) while the translation map is keyed by bare `"cs"` —
    /// matching without that step silently never hits.
    ///
    /// Falls back through name → ISO code → id, so a country seeded without a translation for this
    /// language still renders something a person can read.
    ///
    /// Twin: `CountryListItem.localizedName` (Android, partner-app). Keep the two in step.
    func localizedName(languageTag: String = CoreL10n.languageTag) -> String {
        let translated = UserDefaultsAppSettingsStore.bareLanguageCode(languageTag)
            .flatMap { translations?[$0]?.name }
            .flatMap { $0.isBlank ? nil : $0 }

        return translated
            ?? name.flatMap { $0.isBlank ? nil : $0 }
            ?? isoCode.flatMap { $0.isBlank ? nil : $0 }
            ?? id
            ?? ""
    }
}
