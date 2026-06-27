import CleansiaCore
import CleansiaPartnerApi
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
final class ProfileViewModelTests: XCTestCase {
    private final class StubAuthClient: AuthClient {
        private(set) var logoutCount = 0
        func signOutLocal() async {}
        func logout() async {
            logoutCount += 1
        }
    }

    private var client: FakePartnerProfileClient!
    private var authClient: StubAuthClient!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerProfileClient()
        authClient = StubAuthClient()
        snackbar = SnackbarController()
    }

    private func makeVM() -> ProfileViewModel {
        ProfileViewModel(client: client, authClient: authClient, snackbar: snackbar)
    }

    func testLoadSuccessMapsEmployeeAndContractStatus() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1", firstName: "Jana"))
        client.statusResult = .success(RegistrationCompletionStatus(contractStatus: .approved))
        let vm = makeVM()
        await vm.load()

        guard let data = vm.state.loadedValue else { return XCTFail("expected loaded") }
        XCTAssertEqual(data.employee.firstName, "Jana")
        XCTAssertEqual(data.contractStatus, .approved)
    }

    func testStatusFetchFailureStillLoadsWithNilChip() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1", firstName: "Jana"))
        client.statusResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()

        guard let data = vm.state.loadedValue else { return XCTFail("expected loaded despite status failure") }
        XCTAssertNil(data.contractStatus)
    }

    func testEmployeeLoadFailureSetsErrorAndSnackbars() async {
        client.employeeResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    func testSignOutEmitsEffectAndLogsOut() async {
        let vm = makeVM()
        var emitted = false
        let token = vm.signedOut.sink { emitted = true }
        defer { token.cancel() }

        await vm.signOut()

        XCTAssertTrue(emitted)
        XCTAssertEqual(authClient.logoutCount, 1)
        XCTAssertFalse(vm.action.isSubmitting)
    }
}
