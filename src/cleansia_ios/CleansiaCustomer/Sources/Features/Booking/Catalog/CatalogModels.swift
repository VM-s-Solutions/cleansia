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
    /// The catalogue service this item is. Nil only from a server that does not send it; such an item
    /// marks nothing in the services list and warns about nothing.
    let serviceId: String?
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

/// A pick that books a service twice, held until the customer answers: a package and a service it
/// already includes are both done and both charged, and nothing de-duplicates them
/// (→ /product/business-rules#charging-a-package-and-a-service-together). Only a tap that ADDS asks;
/// a removal, and a selection seeded from elsewhere, never do.
enum TwiceBookedPick: Equatable {
    /// Adding the service `id`, which the selected `packageIds` already include.
    case service(id: String, packageIds: [String])
    /// Adding the package `id`, which includes the selected `serviceIds`.
    case package(id: String, serviceIds: [String])
}

extension Catalog {
    /// The selected packages that include `serviceId`, in catalogue order: what the service's row says
    /// it is already in.
    func selectedPackages(including serviceId: String, selectedPackageIds: Set<String>) -> [CatalogPackage] {
        packages.filter { package in
            selectedPackageIds.contains(package.id) && package.includedServices.contains { $0.serviceId == serviceId }
        }
    }

    /// What adding the service would book twice, or nil when it books nothing twice.
    func twiceBookedPick(addingService serviceId: String, selectedPackageIds: Set<String>) -> TwiceBookedPick? {
        let packageIds = selectedPackages(including: serviceId, selectedPackageIds: selectedPackageIds).map(\.id)
        return packageIds.isEmpty ? nil : .service(id: serviceId, packageIds: packageIds)
    }

    /// What adding the package would book twice, or nil when it books nothing twice.
    func twiceBookedPick(addingPackage packageId: String, selectedServiceIds: Set<String>) -> TwiceBookedPick? {
        guard let package = packages.first(where: { $0.id == packageId }) else { return nil }
        let included = Set(package.includedServices.compactMap(\.serviceId))
        let serviceIds = services.map(\.id).filter { selectedServiceIds.contains($0) && included.contains($0) }
        return serviceIds.isEmpty ? nil : .package(id: packageId, serviceIds: serviceIds)
    }
}
