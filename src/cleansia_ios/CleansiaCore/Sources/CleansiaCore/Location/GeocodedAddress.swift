import Foundation

public struct GeocodedAddress: Equatable {
    public let latitude: Double
    public let longitude: Double
    public let street: String
    public let city: String
    public let zipCode: String
    public let country: String
    public let countryIsoCode: String
    public let formatted: String

    public init(
        latitude: Double,
        longitude: Double,
        street: String,
        city: String,
        zipCode: String,
        country: String,
        countryIsoCode: String,
        formatted: String
    ) {
        self.latitude = latitude
        self.longitude = longitude
        self.street = street
        self.city = city
        self.zipCode = zipCode
        self.country = country
        self.countryIsoCode = countryIsoCode
        self.formatted = formatted
    }
}
