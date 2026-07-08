import CleansiaCore
import CleansiaCustomerApi
import Foundation

protocol ExtraClient {
    func loadExtras() async -> ApiResult<[CatalogExtra]>
}

struct LiveExtraClient: ExtraClient {
    func loadExtras() async -> ApiResult<[CatalogExtra]> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerExtraAPI.extraGetOverview()
        }
        return result.map { items in
            items.compactMap(\.toDomain)
        }
    }
}

private extension ExtraListItem {
    var toDomain: CatalogExtra? {
        guard let id, let slug, let name else { return nil }
        return CatalogExtra(
            id: id,
            slug: slug,
            name: name,
            description: description,
            price: price ?? 0,
            displayOrder: displayOrder ?? 0,
            translations: translations?
                .mapValues { CatalogTranslation(name: $0.name ?? "", description: $0.description) } ?? [:]
        )
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
