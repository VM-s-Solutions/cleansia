import CleansiaCore
import XCTest
@testable import CleansiaCustomer

/// The postcode reaches the city rule: the Prague rows are decided by it, as `CreateOrder` decides them, so an address
/// the server books is never shown as one we do not serve.
final class AddressServiceabilityTests: XCTestCase {
    private struct Source: ServiceAreaDataSource {
        func fetchServicedCountries() async -> ApiResult<[ServicedCountry]> {
            .success([ServicedCountry(id: "cz-id", isoCode: "cz", name: "Czechia")])
        }

        func fetchServicedCities(countryId _: String?) async -> ApiResult<[ServicedCity]> {
            .success([
                ServicedCity(id: "praha", countryId: "cz-id", name: "Praha", zipPrefix: "1"),
                ServicedCity(id: "brno", countryId: "cz-id", name: "Brno", zipPrefix: nil)
            ])
        }
    }

    private let provider = ServiceAreaProvider(dataSource: Source())

    func testAPickedPragueAddressIsServicedByItsPostcodeWhateverItsCityIsCalled() async {
        let serviced = await AddressServiceability.cityServiced(
            provider: provider,
            countryIsoCode: "cz",
            city: "Прага",
            zipCode: "110 00"
        )

        XCTAssertEqual(serviced, true)
    }

    func testAPickedAddressOutsidePragueIsNotPragueWhateverItsCityIsCalled() async {
        let serviced = await AddressServiceability.cityServiced(
            provider: provider,
            countryIsoCode: "cz",
            city: "Praha",
            zipCode: "252 42"
        )

        XCTAssertEqual(serviced, false)
    }

    func testASavedPragueAddressIsServicedByItsPostcodeWhateverItsCityIsCalled() async {
        let serviced = await AddressServiceability.cityServicedAnywhere(
            provider: provider,
            city: "Prag",
            zipCode: "120 00"
        )

        XCTAssertEqual(serviced, true)
    }
}
