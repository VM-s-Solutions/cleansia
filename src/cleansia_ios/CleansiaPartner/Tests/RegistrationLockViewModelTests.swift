import CleansiaCore
import CleansiaPartnerApi
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
final class RegistrationLockViewModelTests: XCTestCase {
    private final class FakeRegistrationClient: PartnerRegistrationClient {
        var result: ApiResult<RegistrationCompletionStatus> = .success(RegistrationCompletionStatus())
        private(set) var callCount = 0

        func checkRegistrationStatus() async -> ApiResult<RegistrationCompletionStatus> {
            callCount += 1
            return result
        }
    }

    private final class FakeAuthClient: AuthClient {
        private(set) var logoutCount = 0
        func signOutLocal() async {}
        func logout() async {
            logoutCount += 1
        }
    }

    private var client: FakeRegistrationClient!
    private var authClient: FakeAuthClient!
    private var cancellables: Set<AnyCancellable>!

    override func setUp() {
        super.setUp()
        client = FakeRegistrationClient()
        authClient = FakeAuthClient()
        cancellables = []
    }

    override func tearDown() {
        client = nil
        authClient = nil
        cancellables = nil
        super.tearDown()
    }

    private func makeViewModel() -> RegistrationLockViewModel {
        RegistrationLockViewModel(client: client, authClient: authClient)
    }

    private func incompleteStatus() -> RegistrationCompletionStatus {
        RegistrationCompletionStatus(
            areDocumentsUploaded: false,
            hasCompletedProfile: true,
            contractStatus: .pending
        )
    }

    private func completeStatus() -> RegistrationCompletionStatus {
        RegistrationCompletionStatus(
            areDocumentsUploaded: true,
            hasCompletedProfile: true,
            contractStatus: .approved
        )
    }

    func testInitialStateIsLoading() {
        XCTAssertTrue(makeViewModel().state.isLoading)
    }

    func testSuccessIncompleteMapsToLoadedSteps() async {
        client.result = .success(incompleteStatus())
        let vm = makeViewModel()

        await vm.load()

        guard let data = vm.state.loadedValue else { return XCTFail("expected loaded") }
        XCTAssertEqual(data.steps.count, 3)
        XCTAssertEqual(data.completedCount, 1)
        XCTAssertNil(data.errorMessage)
    }

    func testFailureWithoutPriorStatusStaysLockedWithError() async {
        client.result = .failure(ApiError(httpStatus: 500))
        let vm = makeViewModel()

        await vm.load()

        guard let data = vm.state.loadedValue else { return XCTFail("expected loaded, not error screen") }
        XCTAssertEqual(data.steps.count, 3)
        XCTAssertTrue(data.steps.allSatisfy { $0.status == .missing })
        XCTAssertNotNil(data.errorMessage)
    }

    func testFailureNeverEntersErrorState() async {
        client.result = .failure(ApiError(httpStatus: 500))
        let vm = makeViewModel()

        await vm.load()

        if case .error = vm.state {
            XCTFail("fail-closed design degrades a load failure to .loaded, never .error")
        }
    }

    func testFailurePreservesLastKnownStatusAndDoesNotUnlock() async {
        client.result = .success(incompleteStatus())
        let vm = makeViewModel()
        await vm.load()
        let knownCompleted = vm.state.loadedValue?.completedCount

        client.result = .failure(ApiError(code: "network.unreachable"))
        await vm.load()

        guard let data = vm.state.loadedValue else { return XCTFail("expected loaded") }
        XCTAssertEqual(data.completedCount, knownCompleted)
        XCTAssertNotNil(data.errorMessage)
        XCTAssertFalse(data.isComplete)
    }

    func testCompleteStatusEmitsCompletedEvent() async {
        client.result = .success(completeStatus())
        let vm = makeViewModel()
        var completed = false
        vm.completed.sink { completed = true }.store(in: &cancellables)

        await vm.load()

        XCTAssertTrue(completed)
    }

    func testIncompleteStatusDoesNotEmitCompletedEvent() async {
        client.result = .success(incompleteStatus())
        let vm = makeViewModel()
        var completed = false
        vm.completed.sink { completed = true }.store(in: &cancellables)

        await vm.load()

        XCTAssertFalse(completed)
    }

    func testSignOutDrivesActionAndLogsOut() async {
        let vm = makeViewModel()

        await vm.signOut()

        XCTAssertEqual(authClient.logoutCount, 1)
        XCTAssertFalse(vm.action.isSubmitting)
    }

    func testMissingFieldsExposedForFixRouting() async {
        client.result = .success(RegistrationCompletionStatus(
            hasCompletedProfile: false,
            missingFields: ["profile.fields.iban"]
        ))
        let vm = makeViewModel()

        await vm.load()

        XCTAssertEqual(vm.missingFields, ["profile.fields.iban"])
    }
}
