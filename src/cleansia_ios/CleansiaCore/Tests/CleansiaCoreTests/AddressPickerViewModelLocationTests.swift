import Combine
import XCTest
@testable import CleansiaCore

@MainActor
final class AddressPickerViewModelLocationTests: XCTestCase {
    private final class FakeLocationProvider: LocationProvider {
        var status: LocationAuthorizationStatus = .notDetermined
        var statusAfterRequest: LocationAuthorizationStatus = .denied
        var fix: Coordinate?
        private(set) var requestCount = 0
        private(set) var currentLocationCount = 0

        var authorizationStatus: LocationAuthorizationStatus {
            status
        }

        func requestWhenInUseAuthorization() async -> LocationAuthorizationStatus {
            requestCount += 1
            status = statusAfterRequest
            return status
        }

        func currentLocation() async -> Coordinate? {
            currentLocationCount += 1
            return fix
        }
    }

    private final class NoopGeocodingService: GeocodingService {
        func reverseGeocode(_: Coordinate) async -> GeocodedAddress? {
            nil
        }

        func forwardGeocode(query _: String, countryIsoCodes _: [String]) async -> [GeocodedAddress] {
            []
        }
    }

    private var provider: FakeLocationProvider!
    private var vm: AddressPickerViewModel!
    private var cancellables: Set<AnyCancellable>!
    private var recenters: [Coordinate]!
    private var failures: [AddressPickerViewModel.LocationFailure]!

    override func setUp() {
        super.setUp()
        provider = FakeLocationProvider()
        vm = AddressPickerViewModel(
            geocoding: NoopGeocodingService(),
            reverseDebounce: .zero,
            searchDebounce: .zero
        )
        cancellables = []
        recenters = []
        failures = []
        vm.recenter.sink { [weak self] in self?.recenters.append($0) }.store(in: &cancellables)
        vm.locationFailed.sink { [weak self] in self?.failures.append($0) }.store(in: &cancellables)
    }

    override func tearDown() {
        cancellables = nil
        vm = nil
        provider = nil
        super.tearDown()
    }

    private let prague = Coordinate(latitude: 50.0755, longitude: 14.4378)

    func testAutoCenterAuthorizedWithFixRecenters() async {
        provider.status = .authorized
        provider.fix = prague

        await vm.autoCenterOnOpen(location: provider)

        XCTAssertEqual(recenters, [prague])
        XCTAssertEqual(failures, [])
        XCTAssertEqual(provider.requestCount, 0)
    }

    func testAutoCenterAuthorizedWithoutFixStaysSilent() async {
        provider.status = .authorized
        provider.fix = nil

        await vm.autoCenterOnOpen(location: provider)

        XCTAssertEqual(recenters, [])
        XCTAssertEqual(failures, [])
    }

    func testAutoCenterNotDeterminedGrantedWithFixRecenters() async {
        provider.status = .notDetermined
        provider.statusAfterRequest = .authorized
        provider.fix = prague

        await vm.autoCenterOnOpen(location: provider)

        XCTAssertEqual(provider.requestCount, 1)
        XCTAssertEqual(recenters, [prague])
        XCTAssertEqual(failures, [])
    }

    func testAutoCenterNotDeterminedGrantedWithoutFixReportsUnavailable() async {
        provider.status = .notDetermined
        provider.statusAfterRequest = .authorized
        provider.fix = nil

        await vm.autoCenterOnOpen(location: provider)

        XCTAssertEqual(recenters, [])
        XCTAssertEqual(failures, [.unavailable])
    }

    func testAutoCenterNotDeterminedDeniedReportsDenied() async {
        provider.status = .notDetermined
        provider.statusAfterRequest = .denied

        await vm.autoCenterOnOpen(location: provider)

        XCTAssertEqual(provider.requestCount, 1)
        XCTAssertEqual(provider.currentLocationCount, 0)
        XCTAssertEqual(recenters, [])
        XCTAssertEqual(failures, [.denied])
    }

    func testAutoCenterAlreadyDeniedStaysSilent() async {
        provider.status = .denied

        await vm.autoCenterOnOpen(location: provider)

        XCTAssertEqual(provider.requestCount, 0)
        XCTAssertEqual(provider.currentLocationCount, 0)
        XCTAssertEqual(recenters, [])
        XCTAssertEqual(failures, [])
    }

    func testAutoCenterAlreadyRestrictedStaysSilent() async {
        provider.status = .restricted

        await vm.autoCenterOnOpen(location: provider)

        XCTAssertEqual(recenters, [])
        XCTAssertEqual(failures, [])
    }

    func testMyLocationAuthorizedWithFixRecenters() async {
        provider.status = .authorized
        provider.fix = prague

        await vm.recenterOnMyLocation(location: provider)

        XCTAssertEqual(recenters, [prague])
        XCTAssertEqual(failures, [])
    }

    func testMyLocationAuthorizedWithoutFixReportsUnavailable() async {
        provider.status = .authorized
        provider.fix = nil

        await vm.recenterOnMyLocation(location: provider)

        XCTAssertEqual(recenters, [])
        XCTAssertEqual(failures, [.unavailable])
    }

    func testMyLocationNotDeterminedGrantedWithFixRecenters() async {
        provider.status = .notDetermined
        provider.statusAfterRequest = .authorized
        provider.fix = prague

        await vm.recenterOnMyLocation(location: provider)

        XCTAssertEqual(provider.requestCount, 1)
        XCTAssertEqual(recenters, [prague])
        XCTAssertEqual(failures, [])
    }

    func testMyLocationNotDeterminedDeniedReportsDenied() async {
        provider.status = .notDetermined
        provider.statusAfterRequest = .denied

        await vm.recenterOnMyLocation(location: provider)

        XCTAssertEqual(recenters, [])
        XCTAssertEqual(failures, [.denied])
    }

    func testMyLocationDeniedReportsDenied() async {
        provider.status = .denied

        await vm.recenterOnMyLocation(location: provider)

        XCTAssertEqual(provider.requestCount, 0)
        XCTAssertEqual(recenters, [])
        XCTAssertEqual(failures, [.denied])
    }

    func testMyLocationRestrictedReportsDenied() async {
        provider.status = .restricted

        await vm.recenterOnMyLocation(location: provider)

        XCTAssertEqual(recenters, [])
        XCTAssertEqual(failures, [.denied])
    }
}
