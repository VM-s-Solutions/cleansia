import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class BankSectionViewModelTests: XCTestCase {
    private var client: FakePartnerProfileClient!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerProfileClient()
        snackbar = SnackbarController()
    }

    private func makeVM() -> BankSectionViewModel {
        BankSectionViewModel(client: client, snackbar: snackbar)
    }

    func testLoadSuccessMapsIban() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1", iban: "CZ65"))
        let vm = makeVM()
        await vm.load()
        guard case .loaded = vm.state else { return XCTFail("expected loaded") }
        XCTAssertEqual(vm.iban, "CZ65")
    }

    func testIbanNormalizesToUppercaseAlphanumeric() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        vm.iban = "cz65 0800-0000"
        XCTAssertEqual(vm.iban, "CZ6508000000")
    }

    func testSaveValidationFailureSkipsNetwork() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        vm.iban = ""
        await vm.save()
        XCTAssertNotNil(vm.ibanError)
        XCTAssertNil(client.bankCommand)
    }

    func testSaveSuccessEmitsSavedEffect() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1", iban: "CZ65"))
        let vm = makeVM()
        await vm.load()

        var emitted = false
        let token = vm.saved.sink { emitted = true }
        defer { token.cancel() }

        await vm.save()
        XCTAssertTrue(emitted)
        XCTAssertEqual(client.bankCommand?.iban, "CZ65")
    }

    func testSaveApiFailureSetsActionError() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1", iban: "CZ65"))
        client.bankUpdateResult = .failure(ApiError(httpStatus: 400))
        let vm = makeVM()
        await vm.load()
        await vm.save()
        guard case .error = vm.action else { return XCTFail("expected action error") }
        XCTAssertNotNil(snackbar.current)
    }
}
