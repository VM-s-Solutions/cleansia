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

struct CatalogPackage: Equatable, Identifiable {
    let id: String
    let name: String
    let description: String?
    let price: Double
    let translations: [String: CatalogTranslation]
    let includedServices: [CatalogPackageServiceSummary]
}

struct Catalog: Equatable {
    let services: [CatalogService]
    let packages: [CatalogPackage]

    static let empty = Catalog(services: [], packages: [])

    var isEmpty: Bool {
        services.isEmpty && packages.isEmpty
    }
}
