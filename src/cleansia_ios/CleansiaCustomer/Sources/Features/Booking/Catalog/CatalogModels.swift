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

/// One row of the platform's currency overview. The catalogue rows above carry no currency of their
/// own — they are priced in the row flagged `isDefault`, which is how every catalogue figure gets its
/// label.
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
    /// The default currency's code — the one every price in `services` and `packages` is stated in.
    let currencyCode: String

    static let empty = Catalog(services: [], packages: [], currencyCode: "")

    var isEmpty: Bool {
        services.isEmpty && packages.isEmpty
    }
}
