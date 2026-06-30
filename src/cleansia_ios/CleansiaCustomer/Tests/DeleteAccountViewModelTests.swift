import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class DeleteAccountViewModelTests: XCTestCase {
    private var gdpr: FakeGdprDeleteClient!
    private var auth: FakeAuthClient!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        gdpr = FakeGdprDeleteClient()
        auth = FakeAuthClient()
        snackbar = SnackbarController()
    }

    private func makeVM() -> DeleteAccountViewModel {
        DeleteAccountViewModel(client: gdpr, authClient: auth, snackbar: snackbar)
    }

    func testSuccessCallsSignOutLocalNotLogout() async {
        gdpr.deleteResult = .success(())
        let vm = makeVM()

        await vm.confirmDelete()

        XCTAssertEqual(auth.signOutLocalCount, 1)
        XCTAssertEqual(auth.logoutCount, 0)
    }

    func testSuccessEmitsAccountDeleted() async {
        gdpr.deleteResult = .success(())
        let vm = makeVM()

        var deleted = false
        let token = vm.accountDeleted.sink { deleted = true }
        defer { token.cancel() }

        await vm.confirmDelete()

        XCTAssertTrue(deleted)
        XCTAssertEqual(vm.deleteState, .idle)
    }

    func testBlockedByOrderShowsErrorAndStaysSignedIn() async {
        gdpr.deleteResult = .failure(ApiError(code: "gdpr.deletion_blocked_by_order", httpStatus: 400))
        let vm = makeVM()

        var deleted = false
        let token = vm.accountDeleted.sink { deleted = true }
        defer { token.cancel() }

        await vm.confirmDelete()

        XCTAssertEqual(auth.signOutLocalCount, 0)
        XCTAssertEqual(auth.logoutCount, 0)
        XCTAssertFalse(deleted)
        XCTAssertNotNil(snackbar.current)
        guard case .error = vm.deleteState else { return XCTFail("expected action error") }
    }

    func testBlockedByInvoiceStaysSignedIn() async {
        gdpr.deleteResult = .failure(ApiError(code: "gdpr.deletion_blocked_by_invoice", httpStatus: 400))
        let vm = makeVM()

        await vm.confirmDelete()

        XCTAssertEqual(auth.signOutLocalCount, 0)
        XCTAssertNotNil(snackbar.current)
        guard case .error = vm.deleteState else { return XCTFail("expected action error") }
    }

    func testAlreadyPendingStaysSignedIn() async {
        gdpr.deleteResult = .failure(ApiError(code: "gdpr.deletion_already_pending", httpStatus: 400))
        let vm = makeVM()

        await vm.confirmDelete()

        XCTAssertEqual(auth.signOutLocalCount, 0)
        XCTAssertNotNil(snackbar.current)
    }

    func testGenericFailureStaysSignedIn() async {
        gdpr.deleteResult = .failure(ApiError(code: "gdpr.deletion_failed", httpStatus: 500))
        let vm = makeVM()

        await vm.confirmDelete()

        XCTAssertEqual(auth.signOutLocalCount, 0)
        guard case .error = vm.deleteState else { return XCTFail("expected action error") }
    }

    func testReentryGuardDropsSecondDeleteWhileSubmitting() async {
        gdpr.deleteResult = .success(())
        let vm = makeVM()

        async let first: Void = vm.confirmDelete()
        async let second: Void = vm.confirmDelete()
        _ = await (first, second)

        XCTAssertEqual(gdpr.deleteCallCount, 1)
    }
}
