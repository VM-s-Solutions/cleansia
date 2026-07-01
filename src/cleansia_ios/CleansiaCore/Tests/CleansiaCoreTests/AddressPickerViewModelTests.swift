import Combine
import XCTest
@testable import CleansiaCore

@MainActor
final class AddressPickerViewModelTests: XCTestCase {
    private final class FakeGeocodingService: GeocodingService {
        var reverseResult: GeocodedAddress?
        var forwardResult: [GeocodedAddress] = []
        private(set) var reverseCallCount = 0
        private(set) var forwardCallCount = 0
        private(set) var lastForwardQuery: String?
        private(set) var lastForwardBias: [String]?

        func reverseGeocode(_: Coordinate) async -> GeocodedAddress? {
            reverseCallCount += 1
            return reverseResult
        }

        func forwardGeocode(query: String, countryIsoCodes: [String]) async -> [GeocodedAddress] {
            forwardCallCount += 1
            lastForwardQuery = query
            lastForwardBias = countryIsoCodes
            return forwardResult
        }
    }

    private var geocoding: FakeGeocodingService!
    private var cancellables: Set<AnyCancellable>!

    override func setUp() {
        super.setUp()
        geocoding = FakeGeocodingService()
        cancellables = []
    }

    override func tearDown() {
        geocoding = nil
        cancellables = nil
        super.tearDown()
    }

    private func makeViewModel(searchBias: [String]? = nil) -> AddressPickerViewModel {
        if let searchBias {
            return AddressPickerViewModel(
                geocoding: geocoding,
                reverseDebounce: .zero,
                searchDebounce: .zero,
                searchBias: searchBias
            )
        }
        return AddressPickerViewModel(
            geocoding: geocoding,
            reverseDebounce: .zero,
            searchDebounce: .zero
        )
    }

    private func address(city: String = "Praha") -> GeocodedAddress {
        GeocodedAddress(
            latitude: 50.0755,
            longitude: 14.4378,
            street: "Vinohradská 12",
            city: city,
            zipCode: "120 00",
            country: "Czechia",
            countryIsoCode: "cz",
            formatted: "Vinohradská 12, \(city)"
        )
    }

    private func waitForSearch(_ vm: AddressPickerViewModel) async {
        for _ in 0 ..< 200 {
            if !vm.searching { return }
            await Task.yield()
            try? await Task.sleep(for: .milliseconds(1))
        }
    }

    func testDefaultSearchBiasIsCzSk() async {
        geocoding.forwardResult = [address()]
        let vm = makeViewModel()

        vm.onSearchChange("Vin")
        await waitForSearch(vm)

        XCTAssertEqual(geocoding.lastForwardBias, ["cz", "sk"])
    }

    func testInjectedSearchBiasIsForwarded() async {
        geocoding.forwardResult = [address()]
        let vm = makeViewModel(searchBias: ["de", "pl"])

        vm.onSearchChange("Ber")
        await waitForSearch(vm)

        XCTAssertEqual(geocoding.lastForwardQuery, "Ber")
        XCTAssertEqual(geocoding.lastForwardBias, ["de", "pl"])
    }
}
