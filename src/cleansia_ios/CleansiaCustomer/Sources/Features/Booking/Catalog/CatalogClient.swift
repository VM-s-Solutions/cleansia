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
            return await apiResult {
                try Catalog(
                    services: serviceItems.map(CatalogService.init),
                    packages: packageItems.map(CatalogPackage.init)
                )
            }
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

/// **Refuse the page.** Two reasons compose here and either alone would decide it. The rows are
/// alternatives to each other — a missing service is a different booking, not a shorter list — and
/// each card renders its own "from X", so a coerced `0` quotes a price the customer will not be
/// charged: the server prices the order again on `Create`, and the difference surfaces after they
/// have committed. Dropping the row hides a service that exists; keeping it at zero misquotes one.
extension CatalogCategory {
    init(_ dto: CategoryDto) throws {
        id = try dto.id.requireNonBlank("id")
        slug = try dto.slug.requireNonBlank("slug")
        name = try dto.name.requireNonBlank("name")
        description = dto.description
        displayOrder = try dto.displayOrder.require("displayOrder")
        translations = dto.translations?.toDomain ?? [:]
    }
}

extension CatalogService {
    init(_ dto: ServiceListItem) throws {
        id = try dto.id.requireNonBlank("id")
        name = try dto.name.requireNonBlank("name")
        description = dto.description
        basePrice = try dto.basePrice.require("basePrice")
        perRoomPrice = try dto.perRoomPrice.require("perRoomPrice")
        category = try CatalogCategory(dto.category.require("category"))
        translations = dto.translations?.toDomain ?? [:]
    }
}

extension CatalogPackageServiceSummary {
    init(_ dto: PackageServiceSummary) throws {
        name = try dto.name.requireNonBlank("name")
        translations = dto.translations?.toDomain ?? [:]
    }
}

extension CatalogPackage {
    init(_ dto: PackageListItem) throws {
        id = try dto.id.requireNonBlank("id")
        name = try dto.name.requireNonBlank("name")
        description = dto.description
        price = try dto.price.require("price")
        translations = dto.translations?.toDomain ?? [:]
        includedServices = try (dto.includedServices ?? []).map(CatalogPackageServiceSummary.init)
    }
}
