import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class AddressSectionViewModelTests: XCTestCase {
    private var client: FakePartnerProfileClient!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerProfileClient()
        snackbar = SnackbarController()
    }

    private func makeVM() -> AddressSectionViewModel {
        AddressSectionViewModel(
            client: client,
            serviceArea: ServiceAreaProvider(dataSource: PartnerServiceAreaDataSource(client: client)),
            snackbar: snackbar
        )
    }

    private func sampleAddress(isoCode: String = "cz") -> GeocodedAddress {
        GeocodedAddress(
            latitude: 50.0755,
            longitude: 14.4378,
            street: "Vinohradská 12",
            city: "Praha",
            zipCode: "120 00",
            country: "Czechia",
            countryIsoCode: isoCode,
            formatted: "Vinohradská 12, Praha"
        )
    }

    func testLoadReconstructsAddressFromEmployee() async {
        client.servicedCountriesResult = .success([
            CountryListItem(id: "cz", isoCode: "CZE", name: "Czechia")
        ])
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            street: "Vinohradská 12",
            city: "Praha",
            zipCode: "120 00",
            countryId: "cz"
        ))
        let vm = makeVM()
        await vm.load()

        guard case .loaded = vm.state else { return XCTFail("expected loaded") }
        XCTAssertEqual(vm.summaryLine1, "Vinohradská 12")
        XCTAssertNotNil(vm.summaryLine2)
        XCTAssertTrue(vm.canSave)
    }

    func testApplyPickUpdatesSummary() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        XCTAssertFalse(vm.canSave)
        vm.applyPick(sampleAddress())
        XCTAssertTrue(vm.canSave)
        XCTAssertEqual(vm.summaryLine1, "Vinohradská 12")
    }

    func testSaveResolvesAlpha3BackendCountryAndSendsCoords() async {
        client.servicedCountriesResult = .success([
            CountryListItem(id: "cz-id", isoCode: "CZE", name: "Czechia")
        ])
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        vm.applyPick(sampleAddress())

        var emitted = false
        let token = vm.saved.sink { emitted = true }
        defer { token.cancel() }

        await vm.save()
        XCTAssertTrue(emitted)
        XCTAssertEqual(client.addressCommand?.countryId, "cz-id")
        XCTAssertEqual(client.addressCommand?.latitude, 50.0755)
        XCTAssertNil(client.addressCommand?.state)
    }

    func testSaveResolvesSlovakiaWhereThePrefixHeuristicFailed() async {
        client.servicedCountriesResult = .success([
            CountryListItem(id: "sk-id", isoCode: "SVK", name: "Slovakia"),
            CountryListItem(id: "pl-id", isoCode: "POL", name: "Poland")
        ])
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        vm.applyPick(sampleAddress(isoCode: "sk"))

        await vm.save()
        XCTAssertEqual(client.addressCommand?.countryId, "sk-id")
    }

    func testSaveWithUnservicedCountrySnackbarsAndSkips() async {
        client.servicedCountriesResult = .success([
            CountryListItem(id: "sk-id", isoCode: "SVK", name: "Slovakia")
        ])
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        vm.applyPick(sampleAddress())
        await vm.save()
        XCTAssertNil(client.addressCommand)
        XCTAssertEqual(snackbar.current?.text, L10n.Profile.errorCountryNotServiced)
        XCTAssertEqual(vm.serviceAreaStatus, .countryNotServiced)
    }

    func testSaveWithoutPickSnackbarsAndSkips() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        await vm.save()
        XCTAssertNil(client.addressCommand)
        XCTAssertNotNil(snackbar.current)
    }

    func testStatusIsServicedAfterLoadReconstructsAlpha3Country() async {
        client.servicedCountriesResult = .success([
            CountryListItem(id: "cz-id", isoCode: "CZE", name: "Czechia")
        ])
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            street: "Vinohradská 12",
            city: "Praha",
            zipCode: "120 00",
            countryId: "cz-id"
        ))
        let vm = makeVM()
        await vm.load()

        XCTAssertEqual(vm.serviceAreaStatus, .countryServiced)
    }

    func testStatusIsUnknownNotBlockedWhenCountriesFetchFails() async {
        client.servicedCountriesResult = .failure(ApiError(code: "network.unreachable"))
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        vm.applyPick(sampleAddress())

        XCTAssertEqual(vm.serviceAreaStatus, .unknown)
    }

    func testSaveRetriesFailedCountriesFetchAndProceeds() async {
        client.servicedCountriesResult = .failure(ApiError(code: "network.unreachable"))
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        vm.applyPick(sampleAddress(isoCode: "sk"))

        client.servicedCountriesResult = .success([
            CountryListItem(id: "sk-id", isoCode: "SVK", name: "Slovakia")
        ])
        await vm.save()

        XCTAssertEqual(client.servicedCountriesCallCount, 2)
        XCTAssertEqual(client.addressCommand?.countryId, "sk-id")
        XCTAssertEqual(vm.serviceAreaStatus, .countryServiced)
    }

    func testSaveReusesTheLoadTimeCountriesWithoutRefetching() async {
        client.servicedCountriesResult = .success([
            CountryListItem(id: "cz-id", isoCode: "CZE", name: "Czechia")
        ])
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        vm.applyPick(sampleAddress())

        await vm.save()

        XCTAssertEqual(client.servicedCountriesCallCount, 1)
        XCTAssertNotNil(client.addressCommand)
    }

    func testSaveWithUnknownCountriesNeverClaimsNotServiced() async {
        client.servicedCountriesResult = .failure(ApiError(code: "network.unreachable"))
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        vm.applyPick(sampleAddress())

        await vm.save()

        XCTAssertNil(client.addressCommand)
        XCTAssertNotNil(snackbar.current)
        XCTAssertNotEqual(snackbar.current?.text, L10n.Profile.errorCountryNotServiced)
        XCTAssertEqual(vm.serviceAreaStatus, .unknown)
    }
}
