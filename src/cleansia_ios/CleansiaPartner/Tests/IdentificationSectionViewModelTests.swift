import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class IdentificationSectionViewModelTests: XCTestCase {
    private var client: FakePartnerProfileClient!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerProfileClient()
        snackbar = SnackbarController()
    }

    private func makeVM() -> IdentificationSectionViewModel {
        IdentificationSectionViewModel(client: client, snackbar: snackbar)
    }

    func testLoadMapsFieldsAndCountryOptions() async {
        client.allCountriesResult = .success([
            CountryListItem(id: "cz", isoCode: "CZE", name: "Czechia")
        ])
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            countryId: "cz",
            nationalityId: "cz",
            passportId: "P123",
            entityType: ._2,
            registrationNumber: "12345678",
            legalEntityName: "Acme s.r.o."
        ))
        let vm = makeVM()
        await vm.load()

        guard case .loaded = vm.state else { return XCTFail("expected loaded") }
        XCTAssertEqual(vm.form.passportId, "P123")
        XCTAssertEqual(vm.form.businessCountryId, "cz")
        XCTAssertTrue(vm.isLegalEntity)
        XCTAssertEqual(vm.countryOptions.first?.label, "Czechia")
    }

    func testSwitchingToNaturalClearsLegalEntityName() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1", entityType: ._2, legalEntityName: "Acme"))
        let vm = makeVM()
        await vm.load()
        vm.setEntityType(._1)
        XCTAssertEqual(vm.form.legalEntityName, "")
    }

    func testSaveValidationFailureSkipsNetwork() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        await vm.save()
        XCTAssertNil(client.identificationCommand)
        XCTAssertNotNil(snackbar.current)
    }

    func testSaveSuccessEmitsSavedEffect() async {
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            countryId: "cz",
            nationalityId: "cz",
            passportId: "P123",
            entityType: ._1,
            registrationNumber: "12345678"
        ))
        let vm = makeVM()
        await vm.load()

        var emitted = false
        let token = vm.saved.sink { emitted = true }
        defer { token.cancel() }

        await vm.save()
        XCTAssertTrue(emitted)
        XCTAssertEqual(client.identificationCommand?.passportId, "P123")
    }

    func testSaveApiFailureSetsActionError() async {
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            countryId: "cz",
            nationalityId: "cz",
            passportId: "P123",
            entityType: ._1,
            registrationNumber: "12345678"
        ))
        client.identificationUpdateResult = .failure(ApiError(httpStatus: 400))
        let vm = makeVM()
        await vm.load()
        await vm.save()
        guard case .error = vm.action else { return XCTFail("expected action error") }
    }
}
