import Foundation

/// Core-owned slim shape for a serviced city — each app maps its generated `ServiceCityDto` into this
/// on the way out of its `ServiceAreaDataSource`, so the shared provider carries no generated-client
/// dependency. Parity with Android `core/servicearea/ServicedCity`.
public struct ServicedCity: Equatable, Sendable {
    public let id: String
    public let countryId: String
    public let name: String
    /// A row that carries one is decided by an address's postcode; see `CityNameMatch`.
    public let zipPrefix: String?

    public init(id: String, countryId: String, name: String, zipPrefix: String?) {
        self.id = id
        self.countryId = countryId
        self.name = name
        self.zipPrefix = zipPrefix
    }
}
