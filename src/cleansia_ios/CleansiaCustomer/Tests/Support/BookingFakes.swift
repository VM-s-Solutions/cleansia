import CleansiaCore
import Foundation
@testable import CleansiaCustomer

final class FakeCatalogClient: CatalogClient, @unchecked Sendable {
    var result: ApiResult<Catalog>
    private(set) var callCount = 0

    init(result: ApiResult<Catalog> = .success(.empty)) {
        self.result = result
    }

    func loadCatalog() async -> ApiResult<Catalog> {
        callCount += 1
        return result
    }
}

final class FakeQuoteClient: QuoteClient, @unchecked Sendable {
    var result: ApiResult<BookingQuote>
    private(set) var callCount = 0
    private(set) var requests: [QuoteRequest] = []

    init(result: ApiResult<BookingQuote> = .success(BookingQuote(totalPrice: 1000, currencyCode: "CZK"))) {
        self.result = result
    }

    func quote(_ request: QuoteRequest) async -> ApiResult<BookingQuote> {
        callCount += 1
        requests.append(request)
        return result
    }
}

enum CatalogFixtures {
    static func category(slug: String = "home", order: Int = 0) -> CatalogCategory {
        CatalogCategory(
            id: "cat-\(slug)",
            slug: slug,
            name: slug.capitalized,
            description: nil,
            displayOrder: order,
            translations: [:]
        )
    }

    static func service(id: String, category: CatalogCategory = category()) -> CatalogService {
        CatalogService(
            id: id,
            name: "Service \(id)",
            description: "desc",
            basePrice: 500,
            perRoomPrice: 100,
            category: category,
            translations: [:]
        )
    }

    static func package(id: String) -> CatalogPackage {
        CatalogPackage(
            id: id,
            name: "Package \(id)",
            description: "desc",
            price: 1500,
            translations: [:],
            includedServices: []
        )
    }

    static let populated = Catalog(
        services: [service(id: "s-1"), service(id: "s-2", category: category(slug: "deep", order: 1))],
        packages: [package(id: "p-1")]
    )
}
