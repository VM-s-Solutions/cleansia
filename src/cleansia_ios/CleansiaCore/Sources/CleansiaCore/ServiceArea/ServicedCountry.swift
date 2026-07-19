import Foundation

/// Core-owned slim shape for a serviced country — each app maps its generated
/// `CountryListItem` into this on the way out of its `ServiceAreaDataSource`,
/// so the shared provider carries no generated-client dependency (the Android
/// `core/servicearea/ServicedCountry` parity).
public struct ServicedCountry: Equatable, Sendable {
    public let id: String
    public let isoCode: String
    public let name: String

    public init(id: String, isoCode: String, name: String) {
        self.id = id
        self.isoCode = isoCode
        self.name = name
    }
}
