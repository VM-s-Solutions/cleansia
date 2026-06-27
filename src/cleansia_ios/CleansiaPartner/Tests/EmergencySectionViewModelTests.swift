import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class EmergencySectionViewModelTests: XCTestCase {
    private var client: FakePartnerProfileClient!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerProfileClient()
        snackbar = SnackbarController()
    }

    private func makeVM() -> EmergencySectionViewModel {
        EmergencySectionViewModel(client: client, snackbar: snackbar)
    }

    func testLoadSuccessMapsFields() async {
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            emergencyContactName: "Petr",
            emergencyContactPhone: "+420999"
        ))
        let vm = makeVM()
        await vm.load()
        guard case .loaded = vm.state else { return XCTFail("expected loaded") }
        XCTAssertEqual(vm.name, "Petr")
        XCTAssertEqual(vm.phone, "+420999")
    }

    func testSaveValidationFailureSkipsNetwork() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        await vm.save()
        XCTAssertNotNil(vm.nameError)
        XCTAssertNotNil(vm.phoneError)
        XCTAssertNil(client.emergencyCommand)
    }

    func testSaveSuccessEmitsSavedEffect() async {
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            emergencyContactName: "Petr",
            emergencyContactPhone: "+420999"
        ))
        let vm = makeVM()
        await vm.load()

        var emitted = false
        let token = vm.saved.sink { emitted = true }
        defer { token.cancel() }

        await vm.save()
        XCTAssertTrue(emitted)
        XCTAssertEqual(client.emergencyCommand?.emergencyName, "Petr")
    }

    func testSaveApiFailureSetsActionError() async {
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            emergencyContactName: "Petr",
            emergencyContactPhone: "+420999"
        ))
        client.emergencyUpdateResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()
        await vm.save()
        guard case .error = vm.action else { return XCTFail("expected action error") }
        XCTAssertNotNil(snackbar.current)
    }
}
