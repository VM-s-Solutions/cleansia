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

    func testLoadFailureSetsErrorAndSnackbars() async {
        client.employeeResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()

        guard case .error = vm.state else { return XCTFail("a failed read must not open an empty form") }
        XCTAssertNotNil(snackbar.current)
    }

    /// The employee id and the pick both survive a failed reload, so nothing else stops the command
    /// from going out — over a profile we could not read.
    func testAFailedReloadRefusesToSaveTheStalePick() async {
        client.servicedCountriesResult = .success([
            CountryListItem(id: "cz-id", isoCode: "CZE", name: "Czechia")
        ])
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        vm.applyPick(sampleAddress())

        client.employeeResult = .failure(ApiError(httpStatus: 500))
        await vm.load()
        await vm.save()

        XCTAssertNil(client.addressCommand)
        XCTAssertEqual(vm.action, .idle)
    }

    func testRetryingAFailedLoadRecoversTheAddress() async {
        client.employeeResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()

        client.employeeResult = .success(EmployeeItem(id: "emp-1", street: "Vinohradská 12", city: "Praha"))
        await vm.load()

        guard case .loaded = vm.state else { return XCTFail("retry left the section in the error state") }
        XCTAssertEqual(vm.summaryLine1, "Vinohradská 12")
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

        // A serviced country is no longer the end of the answer: with the default empty
        // city list, Praha is in a serviced country but not a serviced city.
        XCTAssertEqual(vm.serviceAreaStatus, .outsideServicedCity)
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
        XCTAssertNotEqual(vm.serviceAreaStatus, .countryNotServiced)
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
