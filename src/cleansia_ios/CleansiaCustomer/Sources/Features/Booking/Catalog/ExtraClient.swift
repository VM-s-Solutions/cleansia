import CleansiaCore
import CleansiaCustomerApi
import Foundation

protocol ExtraClient {
    func loadExtras() async -> ApiResult<[CatalogExtra]>
}

struct LiveExtraClient: ExtraClient {
    func loadExtras() async -> ApiResult<[CatalogExtra]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerExtraAPI.extraGetOverview().map(CatalogExtra.init)
        }
    }
}

/// **Refuse the page**, for the catalog's reason: the extras the customer ticks are priced on the
/// booking summary from these very numbers, so a `0` here is an add-on that looks free and is not.
extension CatalogExtra {
    init(_ dto: ExtraListItem) throws {
        id = try dto.id.requireNonBlank("id")
        slug = try dto.slug.requireNonBlank("slug")
        name = try dto.name.requireNonBlank("name")
        description = dto.description
        price = try dto.price.require("price")
        displayOrder = try dto.displayOrder.require("displayOrder")
        translations = dto.translations?
            .mapValues { CatalogTranslation(name: $0.name ?? "", description: $0.description) } ?? [:]
    }
}

extension CatalogExtra {
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
