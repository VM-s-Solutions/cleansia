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
        AddressSectionViewModel(client: client, snackbar: snackbar)
    }

    private func sampleAddress() -> GeocodedAddress {
        GeocodedAddress(
            latitude: 50.0755,
            longitude: 14.4378,
            street: "Vinohradská 12",
            city: "Praha",
            zipCode: "120 00",
            country: "Czechia",
            countryIsoCode: "cz",
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

    func testSaveResolvesCountryByPrefixAndSendsCoords() async {
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
        XCTAssertNotNil(snackbar.current)
    }

    func testSaveWithoutPickSnackbarsAndSkips() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        await vm.save()
        XCTAssertNil(client.addressCommand)
        XCTAssertNotNil(snackbar.current)
    }
}
