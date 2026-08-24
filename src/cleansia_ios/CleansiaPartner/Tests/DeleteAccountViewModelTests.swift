import CleansiaCore
import XCTest
@testable import CleansiaPartner

private final class FakePartnerGdprDeletionClient: PartnerGdprDeletionClient {
    var result: ApiResult<Void> = .success(())
    private(set) var callCount = 0

    func requestDeletion() async -> ApiResult<Void> {
        callCount += 1
        return result
    }
}

/// The partner deletion REQUEST. → /decisions/adr-0052
///
/// The assertions that matter are the negative ones. The endpoint is shared with the customer app
/// and the temptation to mirror the customer flow is exactly how a cleaner would end up signed out
/// of an account that still exists with jobs assigned to them.
@MainActor
final class DeleteAccountViewModelTests: XCTestCase {
    private var client: FakePartnerGdprDeletionClient!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerGdprDeletionClient()
        snackbar = SnackbarController()
    }

    private func makeViewModel() -> DeleteAccountViewModel {
        DeleteAccountViewModel(client: client, snackbar: snackbar)
    }

    func testASuccessfulSubmitFilesTheRequestAndFlipsToRequested() async {
        let vm = makeViewModel()

        await vm.submit()

        XCTAssertTrue(vm.requested)
        XCTAssertEqual(vm.submitState, .idle)
        XCTAssertEqual(client.callCount, 1)
    }

    /// The whole point of the screen. A cleaner keeps working until an admin fulfils the request, so
    /// nothing here may end the session. The view model has no `authClient` collaborator at all,
    /// which is the structural version of this guarantee — this pins that it stays that way by
    /// construction rather than by discipline.
    func testTheViewModelHasNoRouteToASignOut() async {
        let vm = makeViewModel()

        await vm.submit()

        XCTAssertTrue(vm.requested)
        // Reflection over the stored properties: an `AuthClient` appearing here would mean someone
        // wired a sign-out into a screen whose entire purpose is that the session survives.
        let children = Mirror(reflecting: vm).children.compactMap(\.value)
        XCTAssertFalse(
            children.contains { $0 is AuthClient },
            "DeleteAccountViewModel must not hold an AuthClient — filing a request does not end the session"
        )
    }

    func testARefusalSurfacesTheReasonAndStaysUnRequested() async {
        client.result = .failure(ApiError(code: "gdpr.deletion_blocked_by_assigned_order"))
        let vm = makeViewModel()

        await vm.submit()

        XCTAssertFalse(vm.requested)
        XCTAssertEqual(vm.submitState, .error(L10n.DeleteAccount.errorBlockedByAssignedOrder))
    }

    func testUnsettledPayGetsItsOwnWordsRatherThanTheGenericFallback() async {
        client.result = .failure(ApiError(code: "gdpr.deletion_blocked_by_unsettled_pay"))
        let vm = makeViewModel()

        await vm.submit()

        XCTAssertEqual(vm.submitState, .error(L10n.DeleteAccount.errorBlockedByUnsettledPay))
    }

    /// Once filed, the CTA is gone from the UI — but the guard belongs here too, since a restored
    /// navigation entry can call `submit()` again. The server refuses a duplicate anyway; this stops
    /// the pointless round trip and the misleading error it returns.
    func testSubmittingAgainAfterSuccessDoesNotReRequest() async {
        let vm = makeViewModel()

        await vm.submit()
        await vm.submit()

        XCTAssertEqual(client.callCount, 1)
    }
}
