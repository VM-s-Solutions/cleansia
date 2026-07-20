import CleansiaPartnerApi
import Foundation

/// Order-scope name resolution (the Android `ScopeTranslation` parity):
/// app language → `translations[lang].name` → the raw snapshot name. A present
/// translation with a nil OR blank name degrades to the fallback, so the seam
/// is self-sufficient rather than relying on call-site blank guards.
enum ScopeTranslation {
    static func resolveTranslatedName(
        _ translations: [String: Translation]?,
        lang: String?,
        fallback: String?
    ) -> String? {
        guard let lang, !isBlank(lang), let translations, !translations.isEmpty else { return fallback }
        guard let name = translations[lang]?.name, !isBlank(name) else { return fallback }
        return name
    }

    private static func isBlank(_ value: String) -> Bool {
        value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }
}

extension OrderDetailService {
    func localizedName(for locale: Locale) -> String {
        ScopeTranslation.resolveTranslatedName(
            translations,
            lang: locale.language.languageCode?.identifier,
            fallback: name
        ) ?? name
    }
}

extension OrderDetailPackage {
    func localizedName(for locale: Locale) -> String {
        ScopeTranslation.resolveTranslatedName(
            translations,
            lang: locale.language.languageCode?.identifier,
            fallback: name
        ) ?? name
    }
}
