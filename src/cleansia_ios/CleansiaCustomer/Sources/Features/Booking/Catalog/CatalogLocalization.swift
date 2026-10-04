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

extension Catalog {
    /// The service row's "In your package: …" line, naming every selected package that includes it;
    /// nil when none does.
    func inPackageNote(for serviceId: String, selectedPackageIds: Set<String>, locale: Locale) -> String? {
        let names = selectedPackages(including: serviceId, selectedPackageIds: selectedPackageIds)
            .map { $0.localizedName(for: locale) }
        return names.isEmpty ? nil : L10n.Booking.inYourPackage(names.joined(separator: ", "))
    }

    /// The twice-booking confirm's message. A service already in two or more selected packages would be
    /// booked a third time or more, so that case says "once more", never "twice". A package's message
    /// names the services through its own summaries, which also cover one the catalogue does not offer
    /// on its own but another selected package includes.
    func twiceBookedMessage(_ pick: TwiceBookedPick, locale: Locale) -> String {
        switch pick {
        case let .service(id, packageIds):
            let service = services.first { $0.id == id }?.localizedName(for: locale) ?? ""
            let names = packageIds.compactMap { packageId in
                packages.first { $0.id == packageId }?.localizedName(for: locale)
            }.joined(separator: ", ")
            return packageIds.count > 1
                ? L10n.Booking.twiceServiceMessageMany(service: service, packages: names)
                : L10n.Booking.twiceServiceMessage(service: service, packages: names)
        case let .package(id, serviceIds):
            guard let package = packages.first(where: { $0.id == id }) else { return "" }
            let code = CatalogLocalization.languageCode(for: locale)
            let names = package.includedServices
                .filter { $0.serviceId.map(serviceIds.contains) ?? false }
                .map { CatalogLocalization.name(translations: $0.translations, fallback: $0.name, languageCode: code) }
            return L10n.Booking.twicePackageMessage(
                package: package.localizedName(for: locale),
                services: names.joined(separator: ", ")
            )
        }
    }
}
