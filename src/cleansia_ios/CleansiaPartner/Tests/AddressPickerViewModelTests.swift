import CleansiaCore
import Combine
import XCTest
@testable import CleansiaPartner

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

    private func makeViewModel() -> AddressPickerViewModel {
        AddressPickerViewModel(
            geocoding: geocoding,
            reverseDebounce: .zero,
            searchDebounce: .zero
        )
    }

    private func address(
        latitude: Double = 50.0755,
        longitude: Double = 14.4378,
        street: String = "Vinohradská 12",
        city: String = "Praha",
        isoCode: String = "cz"
    ) -> GeocodedAddress {
        GeocodedAddress(
            latitude: latitude,
            longitude: longitude,
            street: street,
            city: city,
            zipCode: "120 00",
            country: "Czechia",
            countryIsoCode: isoCode,
            formatted: "\(street), \(city)"
        )
    }

    private func waitForReverse(_ vm: AddressPickerViewModel) async {
        await pollUntil { !vm.lookingUp }
    }

    private func waitForSearch(_ vm: AddressPickerViewModel) async {
        await pollUntil { !vm.searching }
    }

    private func pollUntil(_ condition: () -> Bool, attempts: Int = 200) async {
        for _ in 0 ..< attempts {
            if condition() { return }
            await Task.yield()
            try? await Task.sleep(for: .milliseconds(1))
        }
    }

    func testInitialStateHasNoResolvedAddress() {
        let vm = makeViewModel()
        XCTAssertNil(vm.resolved)
        XCTAssertFalse(vm.lookingUp)
        XCTAssertFalse(vm.canConfirm)
    }

    func testCenterChangedSetsLookingUpImmediately() {
        let vm = makeViewModel()
        vm.centerChanged(Coordinate(latitude: 50, longitude: 14))
        XCTAssertTrue(vm.lookingUp)
    }

    func testCenterChangedResolvesAfterDebounce() async {
        geocoding.reverseResult = address()
        let vm = makeViewModel()

        vm.centerChanged(Coordinate(latitude: 50, longitude: 14))
        await waitForReverse(vm)

        XCTAssertEqual(vm.resolved, address())
        XCTAssertFalse(vm.lookingUp)
        XCTAssertTrue(vm.canConfirm)
        XCTAssertEqual(geocoding.reverseCallCount, 1)
    }

    func testReverseErrorLeavesResolvedUnchanged() async {
        geocoding.reverseResult = address(street: "First")
        let vm = makeViewModel()
        vm.centerChanged(Coordinate(latitude: 50, longitude: 14))
        await waitForReverse(vm)

        geocoding.reverseResult = nil
        vm.centerChanged(Coordinate(latitude: 51, longitude: 15))
        await waitForReverse(vm)

        XCTAssertEqual(vm.resolved?.street, "First")
        XCTAssertFalse(vm.lookingUp)
    }

    func testSearchUnderTwoCharsDoesNotCallAndClearsResults() async {
        geocoding.forwardResult = [address()]
        let vm = makeViewModel()

        vm.onSearchChange("a")
        await waitForSearch(vm)

        XCTAssertEqual(geocoding.forwardCallCount, 0)
        XCTAssertTrue(vm.searchResults.isEmpty)
        XCTAssertFalse(vm.searching)
    }

    func testSearchTwoOrMoreCharsCallsWithBiasAndPopulatesResults() async {
        geocoding.forwardResult = [address(city: "Praha"), address(city: "Brno")]
        let vm = makeViewModel()

        vm.onSearchChange("Vin")
        XCTAssertTrue(vm.searching)
        await waitForSearch(vm)

        XCTAssertEqual(geocoding.forwardCallCount, 1)
        XCTAssertEqual(geocoding.lastForwardQuery, "Vin")
        XCTAssertEqual(geocoding.lastForwardBias, ["cz", "sk"])
        XCTAssertEqual(vm.searchResults.count, 2)
        XCTAssertFalse(vm.searching)
    }

    func testSelectResultRecentersSetsResolvedAndClearsQuery() {
        let vm = makeViewModel()
        var recentered: Coordinate?
        vm.recenter.sink { recentered = $0 }.store(in: &cancellables)

        let picked = address(latitude: 49.1951, longitude: 16.6068, street: "Náměstí", city: "Brno")
        vm.onSearchChange("Nám")
        vm.selectResult(picked)

        XCTAssertEqual(vm.resolved, picked)
        XCTAssertEqual(vm.searchQuery, "")
        XCTAssertTrue(vm.searchResults.isEmpty)
        XCTAssertEqual(recentered, Coordinate(latitude: 49.1951, longitude: 16.6068))
    }

    func testConfirmFiresConfirmedExactlyOnceWithResolved() async {
        geocoding.reverseResult = address()
        let vm = makeViewModel()
        vm.centerChanged(Coordinate(latitude: 50, longitude: 14))
        await waitForReverse(vm)

        var emitted: [GeocodedAddress] = []
        vm.confirmed.sink { emitted.append($0) }.store(in: &cancellables)

        vm.confirm()

        XCTAssertEqual(emitted, [address()])
    }

    func testConfirmDoesNothingWhenNoResolved() {
        let vm = makeViewModel()
        var emitted = false
        vm.confirmed.sink { _ in emitted = true }.store(in: &cancellables)

        vm.confirm()

        XCTAssertFalse(emitted)
    }

    func testCanConfirmFalseWhileLookingUp() {
        geocoding.reverseResult = address()
        let vm = makeViewModel()
        vm.centerChanged(Coordinate(latitude: 50, longitude: 14))

        XCTAssertTrue(vm.lookingUp)
        XCTAssertFalse(vm.canConfirm)
    }
}
