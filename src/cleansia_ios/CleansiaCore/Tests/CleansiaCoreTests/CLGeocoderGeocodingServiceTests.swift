import CoreLocation
import XCTest
@testable import CleansiaCore

final class CLGeocoderGeocodingServiceTests: XCTestCase {
    private struct StubPlacemark: Placemark {
        var coordinate: Coordinate?
        var thoroughfare: String?
        var subThoroughfare: String?
        var name: String?
        var locality: String?
        var postalCode: String?
        var country: String?
        var isoCountryCode: String?
    }

    private final class FakeGeocoder: CLGeocoding {
        var reverseResult: Result<[Placemark], Error> = .success([])
        var forwardResult: Result<[Placemark], Error> = .success([])
        var isGeocoding = false
        private(set) var cancelCount = 0

        func cancelGeocode() {
            cancelCount += 1
            isGeocoding = false
        }

        func reverseGeocodeLocation(_: CLLocation) async throws -> [Placemark] {
            try reverseResult.get()
        }

        func geocodeAddressString(_: String) async throws -> [Placemark] {
            try forwardResult.get()
        }
    }

    private struct CancelledError: Error {}

    private func placemark(
        latitude: Double = 50.0755,
        longitude: Double = 14.4378,
        thoroughfare: String? = "Vinohradská",
        subThoroughfare: String? = "12",
        name: String? = "Vinohradská 12",
        city: String? = "Praha",
        zip: String? = "120 00",
        country: String? = "Czechia",
        isoCountryCode: String? = "CZ"
    ) -> Placemark {
        StubPlacemark(
            coordinate: Coordinate(latitude: latitude, longitude: longitude),
            thoroughfare: thoroughfare,
            subThoroughfare: subThoroughfare,
            name: name,
            locality: city,
            postalCode: zip,
            country: country,
            isoCountryCode: isoCountryCode
        )
    }

    func testReverseMapsFieldsAndLowercasesIsoCountryCode() async {
        let geocoder = FakeGeocoder()
        geocoder.reverseResult = .success([placemark()])
        let service = CLGeocoderGeocodingService(geocoder: geocoder)

        let address = await service.reverseGeocode(Coordinate(latitude: 50.0755, longitude: 14.4378))

        guard let address else { return XCTFail("expected an address") }
        XCTAssertEqual(address.street, "Vinohradská 12")
        XCTAssertEqual(address.city, "Praha")
        XCTAssertEqual(address.zipCode, "120 00")
        XCTAssertEqual(address.country, "Czechia")
        XCTAssertEqual(address.countryIsoCode, "cz")
        XCTAssertTrue(address.formatted.contains("Vinohradská 12"))
        XCTAssertTrue(address.formatted.contains("Praha"))
    }

    func testReverseStreetUsesThoroughfareWhenNoHouseNumber() async {
        let geocoder = FakeGeocoder()
        geocoder.reverseResult = .success([placemark(subThoroughfare: nil)])
        let service = CLGeocoderGeocodingService(geocoder: geocoder)

        let address = await service.reverseGeocode(Coordinate(latitude: 50, longitude: 14))

        XCTAssertEqual(address?.street, "Vinohradská")
    }

    func testReverseStreetFallsBackToNameWhenNoThoroughfare() async {
        let geocoder = FakeGeocoder()
        geocoder.reverseResult = .success([placemark(thoroughfare: nil, subThoroughfare: nil, name: "Old Town Square")])
        let service = CLGeocoderGeocodingService(geocoder: geocoder)

        let address = await service.reverseGeocode(Coordinate(latitude: 50, longitude: 14))

        XCTAssertEqual(address?.street, "Old Town Square")
    }

    func testReverseUsesPlacemarkCoordinateOverQuery() async {
        let geocoder = FakeGeocoder()
        geocoder.reverseResult = .success([placemark(latitude: 48.1486, longitude: 17.1077)])
        let service = CLGeocoderGeocodingService(geocoder: geocoder)

        let address = await service.reverseGeocode(Coordinate(latitude: 50, longitude: 14))

        XCTAssertEqual(address?.latitude, 48.1486)
        XCTAssertEqual(address?.longitude, 17.1077)
    }

    func testReverseWithNoPlacemarksReturnsNil() async {
        let geocoder = FakeGeocoder()
        geocoder.reverseResult = .success([])
        let service = CLGeocoderGeocodingService(geocoder: geocoder)

        let address = await service.reverseGeocode(Coordinate(latitude: 0, longitude: 0))

        XCTAssertNil(address)
    }

    func testReverseSwallowsErrorAndReturnsNil() async {
        let geocoder = FakeGeocoder()
        geocoder.reverseResult = .failure(CancelledError())
        let service = CLGeocoderGeocodingService(geocoder: geocoder)

        let address = await service.reverseGeocode(Coordinate(latitude: 50, longitude: 14))

        XCTAssertNil(address)
    }

    func testForwardCountryBiasRanksMatchesFirstAndKeepsOthers() async {
        let geocoder = FakeGeocoder()
        geocoder.forwardResult = .success([
            placemark(city: "Wien", country: "Austria", isoCountryCode: "AT"),
            placemark(city: "Praha", country: "Czechia", isoCountryCode: "CZ"),
            placemark(city: "Bratislava", country: "Slovakia", isoCountryCode: "SK")
        ])
        let service = CLGeocoderGeocodingService(geocoder: geocoder)

        let results = await service.forwardGeocode(query: "test", countryIsoCodes: ["cz", "sk"])

        XCTAssertEqual(results.map(\.countryIsoCode), ["cz", "sk", "at"])
    }

    func testForwardBiasAcceptsBackendAlpha3Codes() async {
        let geocoder = FakeGeocoder()
        geocoder.forwardResult = .success([
            placemark(city: "Wien", country: "Austria", isoCountryCode: "AT"),
            placemark(city: "Bratislava", country: "Slovakia", isoCountryCode: "SK")
        ])
        let service = CLGeocoderGeocodingService(geocoder: geocoder)

        let results = await service.forwardGeocode(query: "test", countryIsoCodes: ["SVK"])

        XCTAssertEqual(results.map(\.countryIsoCode), ["sk", "at"])
    }

    func testForwardWithEmptyBiasReturnsAll() async {
        let geocoder = FakeGeocoder()
        geocoder.forwardResult = .success([
            placemark(isoCountryCode: "CZ"),
            placemark(isoCountryCode: "AT")
        ])
        let service = CLGeocoderGeocodingService(geocoder: geocoder)

        let results = await service.forwardGeocode(query: "test", countryIsoCodes: [])

        XCTAssertEqual(results.count, 2)
    }

    func testForwardWithBlankQueryReturnsEmptyWithoutCalling() async {
        let geocoder = FakeGeocoder()
        geocoder.forwardResult = .failure(CancelledError())
        let service = CLGeocoderGeocodingService(geocoder: geocoder)

        let results = await service.forwardGeocode(query: "   ", countryIsoCodes: ["cz"])

        XCTAssertTrue(results.isEmpty)
    }

    func testCancelsInFlightBeforeReverse() async {
        let geocoder = FakeGeocoder()
        geocoder.isGeocoding = true
        geocoder.reverseResult = .success([placemark()])
        let service = CLGeocoderGeocodingService(geocoder: geocoder)

        _ = await service.reverseGeocode(Coordinate(latitude: 50, longitude: 14))

        XCTAssertEqual(geocoder.cancelCount, 1)
    }

    func testCancelledGeocodeReturnsNilNotCrash() async {
        let geocoder = FakeGeocoder()
        geocoder.reverseResult = .failure(CLError(.geocodeCanceled))
        let service = CLGeocoderGeocodingService(geocoder: geocoder)

        let address = await service.reverseGeocode(Coordinate(latitude: 50, longitude: 14))

        XCTAssertNil(address)
    }
}
