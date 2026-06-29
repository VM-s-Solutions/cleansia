import Foundation

enum CatalogLocalization {
    static func name(
        translations: [String: CatalogTranslation],
        fallback: String,
        languageCode: String = Locale.current.language.languageCode?.identifier ?? "en"
    ) -> String {
        translations[languageCode]?.name ?? fallback
    }

    static func description(
        translations: [String: CatalogTranslation],
        fallback: String?,
        languageCode: String = Locale.current.language.languageCode?.identifier ?? "en"
    ) -> String? {
        translations[languageCode]?.description ?? fallback
    }
}

extension CatalogCategory {
    var localizedName: String {
        CatalogLocalization.name(translations: translations, fallback: name)
    }
}

extension CatalogService {
    var localizedName: String {
        CatalogLocalization.name(translations: translations, fallback: name)
    }

    var localizedDescription: String? {
        CatalogLocalization.description(translations: translations, fallback: description)
    }
}

extension CatalogPackage {
    var localizedName: String {
        CatalogLocalization.name(translations: translations, fallback: name)
    }

    var localizedDescription: String? {
        CatalogLocalization.description(translations: translations, fallback: description)
    }

    var includesSummary: String? {
        guard !includedServices.isEmpty else { return nil }
        let names = includedServices.map {
            CatalogLocalization.name(translations: $0.translations, fallback: $0.name)
        }
        let prefix = L10n.Booking.packageIncludesPrefix
        if names.count <= 2 {
            return "\(prefix) \(names.joined(separator: ", "))"
        }
        return "\(prefix) \(names.prefix(2).joined(separator: ", ")) \(L10n.Booking.packageMore(names.count - 2))"
    }
}
