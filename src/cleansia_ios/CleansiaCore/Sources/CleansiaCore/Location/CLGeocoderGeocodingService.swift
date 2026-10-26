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
    func reverseGeocodeLocation(_ location: CLLocation, preferredLocale: Locale) async throws -> [Placemark]
    func geocodeAddressString(_ addressString: String, preferredLocale: Locale) async throws -> [Placemark]
}

extension CLGeocoder: CLGeocoding {
    func reverseGeocodeLocation(_ location: CLLocation, preferredLocale: Locale) async throws -> [Placemark] {
        let placemarks: [CLPlacemark] = try await reverseGeocodeLocation(location, preferredLocale: preferredLocale)
        return placemarks
    }

    func geocodeAddressString(_ addressString: String, preferredLocale: Locale) async throws -> [Placemark] {
        let placemarks: [CLPlacemark] = try await geocodeAddressString(
            addressString,
            in: nil,
            preferredLocale: preferredLocale
        )
        return placemarks
    }
}

public final class CLGeocoderGeocodingService: GeocodingService {
    /// The city is matched against the serviced-city rows, which name it as the Czech post does: asked in the phone's
    /// language, a Prague address came back as "Прага" or "Prag" and matched none. → /mobile-app/patterns#maps
    private static let placeNames = Locale(identifier: "cs_CZ")

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
        let placemarks = try? await geocoder.reverseGeocodeLocation(location, preferredLocale: Self.placeNames)
        guard let placemark = placemarks?.first else {
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
        let found = try? await geocoder.geocodeAddressString(trimmed, preferredLocale: Self.placeNames)
        guard let placemarks = found else {
            return []
        }
        let addresses = placemarks.compactMap { Self.address(from: $0) }
        let bias = Set(countryIsoCodes.map { IsoCountryCodes.toAlpha2($0) }.filter { !$0.isEmpty })
        guard !bias.isEmpty else { return addresses }
        // The bias RANKS, never filters: CLGeocoder has no country parameter,
        // and dropping non-matching placemarks made every search outside the
        // biased countries return nothing (the backend re-validates the
        // country on save, so unbiased results are safe to surface).
        let biased = addresses.filter { bias.contains($0.countryIsoCode) }
        return biased + addresses.filter { !bias.contains($0.countryIsoCode) }
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
