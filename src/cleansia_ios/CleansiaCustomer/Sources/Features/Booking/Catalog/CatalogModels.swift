import Foundation

struct CatalogTranslation: Equatable {
    let name: String
    let description: String?
}

struct CatalogCategory: Equatable, Identifiable {
    let id: String
    let slug: String
    let name: String
    let description: String?
    let displayOrder: Int
    let translations: [String: CatalogTranslation]
}

struct CatalogService: Equatable, Identifiable {
    let id: String
    let name: String
    let description: String?
    let basePrice: Double
    let perRoomPrice: Double
    let category: CatalogCategory
    let translations: [String: CatalogTranslation]
}

struct CatalogPackageServiceSummary: Equatable {
    let name: String
    let translations: [String: CatalogTranslation]
}

struct CatalogExtra: Equatable, Identifiable {
    let id: String
    let slug: String
    let name: String
    let description: String?
    let price: Double
    let displayOrder: Int
    let translations: [String: CatalogTranslation]
}

struct CatalogPackage: Equatable, Identifiable {
    let id: String
    let name: String
    let description: String?
    let price: Double
    let translations: [String: CatalogTranslation]
    let includedServices: [CatalogPackageServiceSummary]
}

/// One row of the platform's currency overview. The row flagged `isDefault` is the platform default:
/// what a figure that arrives with no currency of its own (a membership plan) is stated in.
struct CatalogCurrency: Equatable, Identifiable {
    let id: String
    let code: String
    let symbol: String
    let name: String
    let isDefault: Bool
}

struct Catalog: Equatable {
    let services: [CatalogService]
    let packages: [CatalogPackage]
    /// The code every price in `services` and `packages` is stated in — the market's currency.
    let currencyCode: String
    /// The platform default's code, for the figures that arrive without a currency of their own.
    let defaultCurrencyCode: String

    static let empty = Catalog(services: [], packages: [], currencyCode: "", defaultCurrencyCode: "")

    var isEmpty: Bool {
        services.isEmpty && packages.isEmpty
    }
}
