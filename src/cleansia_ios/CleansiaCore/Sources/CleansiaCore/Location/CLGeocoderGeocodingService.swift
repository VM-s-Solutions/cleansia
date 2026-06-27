import CoreLocation
import Foundation

protocol Placemark {
    var coordinate: Coordinate? { get }
    var thoroughfare: String? { get }
    var subThoroughfare: String? { get }
    var name: String? { get }
    var locality: String? { get }
    var postalCode: String? { get }
    var country: String? { get }
    var isoCountryCode: String? { get }
}

extension CLPlacemark: Placemark {
    var coordinate: Coordinate? {
        location.map { Coordinate(latitude: $0.coordinate.latitude, longitude: $0.coordinate.longitude) }
    }
}

protocol CLGeocoding: AnyObject {
    var isGeocoding: Bool { get }
    func cancelGeocode()
    func reverseGeocodeLocation(_ location: CLLocation) async throws -> [Placemark]
    func geocodeAddressString(_ addressString: String) async throws -> [Placemark]
}

extension CLGeocoder: CLGeocoding {
    func reverseGeocodeLocation(_ location: CLLocation) async throws -> [Placemark] {
        let placemarks: [CLPlacemark] = try await reverseGeocodeLocation(location)
        return placemarks
    }

    func geocodeAddressString(_ addressString: String) async throws -> [Placemark] {
        let placemarks: [CLPlacemark] = try await geocodeAddressString(addressString)
        return placemarks
    }
}

public final class CLGeocoderGeocodingService: GeocodingService {
    private let geocoder: CLGeocoding

    public convenience init() {
        self.init(geocoder: CLGeocoder())
    }

    init(geocoder: CLGeocoding) {
        self.geocoder = geocoder
    }

    public func reverseGeocode(_ coordinate: Coordinate) async -> GeocodedAddress? {
        cancelInFlight()
        let location = CLLocation(latitude: coordinate.latitude, longitude: coordinate.longitude)
        guard let placemark = try? await geocoder.reverseGeocodeLocation(location).first else {
            return nil
        }
        return Self.address(
            from: placemark,
            fallbackLatitude: coordinate.latitude,
            fallbackLongitude: coordinate.longitude
        )
    }

    public func forwardGeocode(query: String, countryIsoCodes: [String]) async -> [GeocodedAddress] {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return [] }
        cancelInFlight()
        guard let placemarks = try? await geocoder.geocodeAddressString(trimmed) else {
            return []
        }
        let addresses = placemarks.compactMap { Self.address(from: $0) }
        let bias = countryIsoCodes.map { $0.lowercased() }
        guard !bias.isEmpty else { return addresses }
        return addresses.filter { bias.contains($0.countryIsoCode) }
    }

    private func cancelInFlight() {
        if geocoder.isGeocoding {
            geocoder.cancelGeocode()
        }
    }

    private static func address(
        from placemark: Placemark,
        fallbackLatitude: Double? = nil,
        fallbackLongitude: Double? = nil
    ) -> GeocodedAddress? {
        let latitude = placemark.coordinate?.latitude ?? fallbackLatitude
        let longitude = placemark.coordinate?.longitude ?? fallbackLongitude
        guard let latitude, let longitude else { return nil }
        return GeocodedAddress(
            latitude: latitude,
            longitude: longitude,
            street: street(from: placemark),
            city: placemark.locality ?? "",
            zipCode: placemark.postalCode ?? "",
            country: placemark.country ?? "",
            countryIsoCode: placemark.isoCountryCode?.lowercased() ?? "",
            formatted: formatted(from: placemark)
        )
    }

    private static func street(from placemark: Placemark) -> String {
        let base = placemark.thoroughfare ?? ""
        let houseNumber = placemark.subThoroughfare ?? ""
        switch (base.isEmpty, houseNumber.isEmpty) {
        case (false, false): return "\(base) \(houseNumber)"
        case (false, true): return base
        default: return placemark.name ?? ""
        }
    }

    private static func formatted(from placemark: Placemark) -> String {
        let parts = [
            street(from: placemark),
            placemark.postalCode,
            placemark.locality,
            placemark.country
        ]
        return parts
            .compactMap { $0 }
            .filter { !$0.isEmpty }
            .joined(separator: ", ")
    }
}
