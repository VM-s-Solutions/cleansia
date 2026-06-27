import Foundation

public protocol GeocodingService {
    func reverseGeocode(_ coordinate: Coordinate) async -> GeocodedAddress?
    func forwardGeocode(query: String, countryIsoCodes: [String]) async -> [GeocodedAddress]
}
