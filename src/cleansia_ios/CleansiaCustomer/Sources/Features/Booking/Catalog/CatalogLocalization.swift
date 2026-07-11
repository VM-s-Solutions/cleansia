import Foundation

enum CatalogLocalization {
    static func name(
        translations: [String: CatalogTranslation],
        fallback: String,
        languageCode: String
    ) -> String {
        translations[languageCode]?.name ?? fallback
    }

    static func description(
        translations: [String: CatalogTranslation],
        fallback: String?,
        languageCode: String
    ) -> String? {
        translations[languageCode]?.description ?? fallback
    }

    static func languageCode(for locale: Locale) -> String {
        locale.language.languageCode?.identifier ?? "en"
    }
}

extension CatalogCategory {
    func localizedName(for locale: Locale) -> String {
        CatalogLocalization.name(
            translations: translations,
            fallback: name,
            languageCode: CatalogLocalization.languageCode(for: locale)
        )
    }
}

extension CatalogService {
    func localizedName(for locale: Locale) -> String {
        CatalogLocalization.name(
            translations: translations,
            fallback: name,
            languageCode: CatalogLocalization.languageCode(for: locale)
        )
    }

    func localizedDescription(for locale: Locale) -> String? {
        CatalogLocalization.description(
            translations: translations,
            fallback: description,
            languageCode: CatalogLocalization.languageCode(for: locale)
        )
    }
}

extension CatalogPackage {
    func localizedName(for locale: Locale) -> String {
        CatalogLocalization.name(
            translations: translations,
            fallback: name,
            languageCode: CatalogLocalization.languageCode(for: locale)
        )
    }

    func localizedDescription(for locale: Locale) -> String? {
        CatalogLocalization.description(
            translations: translations,
            fallback: description,
            languageCode: CatalogLocalization.languageCode(for: locale)
        )
    }

    func includesSummary(for locale: Locale) -> String? {
        guard !includedServices.isEmpty else { return nil }
        let code = CatalogLocalization.languageCode(for: locale)
        let names = includedServices.map {
            CatalogLocalization.name(translations: $0.translations, fallback: $0.name, languageCode: code)
        }
        let prefix = L10n.Booking.packageIncludesPrefix
        if names.count <= 2 {
            return "\(prefix) \(names.joined(separator: ", "))"
        }
        return "\(prefix) \(names.prefix(2).joined(separator: ", ")) \(L10n.Booking.packageMore(names.count - 2))"
    }
}
