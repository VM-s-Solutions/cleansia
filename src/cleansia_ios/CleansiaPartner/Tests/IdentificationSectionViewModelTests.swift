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

    func testLoadFailureSetsErrorAndSnackbars() async {
        client.employeeResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()

        guard case .error = vm.state else { return XCTFail("a failed read must not open an empty form") }
        XCTAssertNotNil(snackbar.current)
    }

    /// A countries failure is survivable — the employee read is not; only the second one is fatal.
    func testACountriesFailureStillLoadsTheForm() async {
        client.allCountriesResult = .failure(ApiError(httpStatus: 500))
        client.employeeResult = .success(EmployeeItem(id: "emp-1", nationalityId: "cz", passportId: "P123"))
        let vm = makeVM()
        await vm.load()

        guard case .loaded = vm.state else { return XCTFail("a countries failure is not fatal") }
        XCTAssertTrue(vm.countryOptions.isEmpty)
        XCTAssertEqual(vm.form.passportId, "P123")
    }

    /// The employee id survives a failed reload, so nothing else stops the command from going out
    /// carrying whatever the form happens to hold — over a profile we could not read.
    func testAFailedReloadRefusesToSaveTheStaleForm() async {
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

        client.employeeResult = .failure(ApiError(httpStatus: 500))
        await vm.load()
        await vm.save()

        XCTAssertNil(client.identificationCommand)
        XCTAssertEqual(vm.action, .idle)
    }

    func testRetryingAFailedLoadRecoversTheForm() async {
        client.employeeResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()

        client.employeeResult = .success(EmployeeItem(id: "emp-1", passportId: "P123"))
        await vm.load()

        guard case .loaded = vm.state else { return XCTFail("retry left the section in the error state") }
        XCTAssertEqual(vm.form.passportId, "P123")
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
