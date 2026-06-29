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

final class FakeExtraClient: ExtraClient, @unchecked Sendable {
    var result: ApiResult<[CatalogExtra]>
    private(set) var callCount = 0

    init(result: ApiResult<[CatalogExtra]> = .success([])) {
        self.result = result
    }

    func loadExtras() async -> ApiResult<[CatalogExtra]> {
        callCount += 1
        return result
    }
}

final class FakePromoCodeClient: PromoCodeClient, @unchecked Sendable {
    var result: ApiResult<PromoValidation>
    private(set) var callCount = 0
    private(set) var lastCode: String?
    private(set) var lastSubtotal: Double?

    init(result: ApiResult<PromoValidation> = .success(PromoValidation(
        isValid: true,
        discountAmount: 100,
        errorCode: nil
    ))) {
        self.result = result
    }

    func validate(code: String, orderSubtotal: Double) async -> ApiResult<PromoValidation> {
        callCount += 1
        lastCode = code
        lastSubtotal = orderSubtotal
        return result
    }
}

final class FakeReferralClient: ReferralClient, @unchecked Sendable {
    var result: ApiResult<ReferralValidation>
    private(set) var callCount = 0
    private(set) var lastCode: String?

    init(result: ApiResult<ReferralValidation> = .success(ReferralValidation(
        isValid: true,
        referrerFirstName: "Eva",
        errorCode: nil
    ))) {
        self.result = result
    }

    func validate(code: String) async -> ApiResult<ReferralValidation> {
        callCount += 1
        lastCode = code
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

    static func extra(slug: String, order: Int = 0) -> CatalogExtra {
        CatalogExtra(
            id: "extra-\(slug)",
            slug: slug,
            name: slug.capitalized,
            description: "desc",
            price: 200,
            displayOrder: order,
            translations: [:]
        )
    }

    static let extras = [
        extra(slug: "inside-oven", order: 1),
        extra(slug: "windows", order: 0)
    ]
}
