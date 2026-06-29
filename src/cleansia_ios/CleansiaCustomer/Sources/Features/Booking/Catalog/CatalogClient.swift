import CleansiaCore
import CleansiaCustomerApi
import Foundation

protocol CatalogClient {
    func loadCatalog() async -> ApiResult<Catalog>
}

struct LiveCatalogClient: CatalogClient {
    func loadCatalog() async -> ApiResult<Catalog> {
        async let servicesCall = apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerServiceAPI.serviceGetOverview()
        }
        async let packagesCall = apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerPackageAPI.packageGetOverview()
        }

        let services = await servicesCall
        let packages = await packagesCall

        switch (services, packages) {
        case let (.failure(error), _):
            return .failure(error)
        case let (_, .failure(error)):
            return .failure(error)
        case let (.success(serviceItems), .success(packageItems)):
            return .success(Catalog(
                services: serviceItems.compactMap(\.toDomain),
                packages: packageItems.compactMap(\.toDomain)
            ))
        }
    }
}

private extension Translation {
    var toDomain: CatalogTranslation {
        CatalogTranslation(name: name ?? "", description: description)
    }
}

private extension [String: Translation] {
    var toDomain: [String: CatalogTranslation] {
        mapValues(\.toDomain)
    }
}

private extension CategoryDto {
    var toDomain: CatalogCategory? {
        guard let id, let slug, let name else { return nil }
        return CatalogCategory(
            id: id,
            slug: slug,
            name: name,
            description: description,
            displayOrder: displayOrder ?? 0,
            translations: translations?.toDomain ?? [:]
        )
    }
}

private extension ServiceListItem {
    var toDomain: CatalogService? {
        guard let id, let name, let category = category?.toDomain else { return nil }
        return CatalogService(
            id: id,
            name: name,
            description: description,
            basePrice: basePrice ?? 0,
            perRoomPrice: perRoomPrice ?? 0,
            category: category,
            translations: translations?.toDomain ?? [:]
        )
    }
}

private extension PackageServiceSummary {
    var toDomain: CatalogPackageServiceSummary? {
        guard let name else { return nil }
        return CatalogPackageServiceSummary(name: name, translations: translations?.toDomain ?? [:])
    }
}

private extension PackageListItem {
    var toDomain: CatalogPackage? {
        guard let id, let name else { return nil }
        return CatalogPackage(
            id: id,
            name: name,
            description: description,
            price: price ?? 0,
            translations: translations?.toDomain ?? [:],
            includedServices: includedServices?.compactMap(\.toDomain) ?? []
        )
    }
}
