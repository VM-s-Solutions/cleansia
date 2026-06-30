import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
final class CreateDisputeViewModelTests: XCTestCase {
    private let validDescription = "Cleaner skipped the kitchen entirely"
    private var cancellables: Set<AnyCancellable> = []

    private func makeVM(
        orderId: String? = "order-1",
        client: FakeDisputeClient
    ) -> (CreateDisputeViewModel, DisputeRepository) {
        let repo = DisputeRepository(client: client, pageSize: 1)
        let vm = CreateDisputeViewModel(orderId: orderId, repository: repo, snackbar: SnackbarController())
        return (vm, repo)
    }

    func testStartsIdle() {
        let (vm, _) = makeVM(client: FakeDisputeClient())
        XCTAssertEqual(vm.submitState, .idle)
    }

    func testSubmitSuccessEmitsCreatedIdAndReturnsIdleAndRefreshes() async {
        let client = FakeDisputeClient()
        client.createResult = .success("dispute-9")
        let (vm, _) = makeVM(client: client)

        var emitted: String?
        vm.created.sink { emitted = $0 }.store(in: &cancellables)

        await vm.submit(reason: 3, description: validDescription)

        XCTAssertEqual(emitted, "dispute-9")
        XCTAssertEqual(vm.submitState, .idle)
        XCTAssertEqual(client.lastCreate?.orderId, "order-1")
        XCTAssertEqual(client.lastCreate?.reason, 3)
        XCTAssertGreaterThanOrEqual(client.pageRequests.count, 1) // refresh fired
    }

    func testSubmitTrimsDescription() async {
        let client = FakeDisputeClient()
        let (vm, _) = makeVM(client: client)

        await vm.submit(reason: 3, description: "   \(validDescription)   ")

        XCTAssertEqual(client.lastCreate?.description, validDescription)
    }

    func testSubmitFailureSurfacesInlineRetryHint() async {
        let client = FakeDisputeClient()
        client.createResult = .failure(ApiError(httpStatus: 500))
        let (vm, _) = makeVM(client: client)

        await vm.submit(reason: 3, description: validDescription)

        XCTAssertEqual(vm.submitState, .error(L10n.Disputes.createRetryHint))
    }

    func testMissingOrderIdSurfacesErrorWithoutCallingRepo() async {
        let client = FakeDisputeClient()
        let (vm, _) = makeVM(orderId: nil, client: client)

        await vm.submit(reason: 3, description: validDescription)

        XCTAssertEqual(vm.submitState, .error(L10n.Disputes.createMissingOrder))
        XCTAssertEqual(client.createCallCount, 0)
        XCTAssertFalse(vm.hasOrderContext)
    }

    func testBlankOrderIdTreatedAsMissing() async {
        let client = FakeDisputeClient()
        let (vm, _) = makeVM(orderId: "   ", client: client)

        await vm.submit(reason: 3, description: validDescription)

        XCTAssertEqual(vm.submitState, .error(L10n.Disputes.createMissingOrder))
        XCTAssertEqual(client.createCallCount, 0)
    }

    func testTooShortDescriptionDoesNotSubmit() async {
        let client = FakeDisputeClient()
        let (vm, _) = makeVM(client: client)

        await vm.submit(reason: 3, description: "too short") // < 10 chars trimmed

        XCTAssertEqual(client.createCallCount, 0)
    }

    func testTooLongDescriptionDoesNotSubmit() async {
        let client = FakeDisputeClient()
        let (vm, _) = makeVM(client: client)

        await vm.submit(reason: 3, description: String(repeating: "a", count: 2001))

        XCTAssertEqual(client.createCallCount, 0)
    }

    func testDescriptionValidationBounds() {
        let (vm, _) = makeVM(client: FakeDisputeClient())
        XCTAssertFalse(vm.descriptionIsValid(String(repeating: "a", count: 9)))
        XCTAssertTrue(vm.descriptionIsValid(String(repeating: "a", count: 10)))
        XCTAssertTrue(vm.descriptionIsValid(String(repeating: "a", count: 2000)))
        XCTAssertFalse(vm.descriptionIsValid(String(repeating: "a", count: 2001)))
    }

    func testClearErrorResetsToIdle() async {
        let client = FakeDisputeClient()
        client.createResult = .failure(ApiError(httpStatus: 500))
        let (vm, _) = makeVM(client: client)
        await vm.submit(reason: 3, description: validDescription)
        guard case .error = vm.submitState else { return XCTFail("expected error") }

        vm.clearError()

        XCTAssertEqual(vm.submitState, .idle)
    }
}
