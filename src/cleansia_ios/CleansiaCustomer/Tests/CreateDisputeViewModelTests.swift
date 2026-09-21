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
        client: FakeDisputeClient,
        snackbar: SnackbarController? = nil
    ) -> (CreateDisputeViewModel, DisputeRepository) {
        let repo = DisputeRepository(client: client, pageSize: 1)
        let vm = CreateDisputeViewModel(
            orderId: orderId,
            repository: repo,
            // These cases are about the submit path; the item list has its own test.
            orderClient: FakeOrderClient(),
            snackbar: snackbar ?? SnackbarController()
        )
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

    // MARK: - Evidence picked before the dispute exists

    private func pdf(_ tag: String = "a") -> EvidenceSource {
        .pdf(Data("%PDF-1.4 \(tag)".utf8))
    }

    private func exists(_ url: URL) -> Bool {
        FileManager.default.fileExists(atPath: url.path)
    }

    func testAddEvidenceKeepsAValidFileAsPending() {
        let (vm, _) = makeVM(client: FakeDisputeClient())

        vm.addEvidence(pdf(), fileName: "receipt.pdf")

        XCTAssertEqual(vm.pickedEvidence.map(\.fileName), ["receipt.pdf"])
        XCTAssertEqual(vm.pickedEvidence.first?.upload, .pending)
        XCTAssertEqual(vm.pickedEvidence.first?.isPdf, true)
        XCTAssertEqual(vm.pickedEvidence.first.map { exists($0.file.url) }, true)
    }

    func testAddEvidenceRefusesAnOversizedFile() {
        let snackbar = SnackbarController()
        let (vm, _) = makeVM(client: FakeDisputeClient(), snackbar: snackbar)

        vm.addEvidence(.pdf(Data(count: DisputeFormConstants.maxEvidenceBytes + 1)), fileName: "big.pdf")

        XCTAssertTrue(vm.pickedEvidence.isEmpty)
        XCTAssertEqual(snackbar.current?.text, L10n.Disputes.evidenceTooLarge)
    }

    func testRemoveEvidenceDropsOnlyThatFileAndItsTempCopy() {
        let (vm, _) = makeVM(client: FakeDisputeClient())
        vm.addEvidence(pdf("a"), fileName: "a.pdf")
        vm.addEvidence(pdf("r"), fileName: "r.pdf")
        guard let first = vm.pickedEvidence.first else { return XCTFail("expected two files") }

        vm.removeEvidence(id: first.id)

        XCTAssertEqual(vm.pickedEvidence.map(\.fileName), ["r.pdf"])
        XCTAssertFalse(exists(first.file.url))
    }

    func testSubmitCreatesThenUploadsEachFileInOrderThenEmitsTheId() async {
        let client = FakeDisputeClient()
        client.createResult = .success("dispute-9")
        let snackbar = SnackbarController()
        let (vm, _) = makeVM(client: client, snackbar: snackbar)
        vm.addEvidence(pdf("a"), fileName: "a.pdf")
        vm.addEvidence(pdf("r"), fileName: "r.pdf")
        let urls = vm.pickedEvidence.map(\.file.url)
        var emitted: String?
        vm.created.sink { emitted = $0 }.store(in: &cancellables)

        await vm.submit(reason: 3, description: validDescription)

        XCTAssertEqual(client.createCallCount, 1)
        XCTAssertEqual(client.uploadedFiles, urls)
        XCTAssertEqual(client.uploadedDisputeIds, ["dispute-9", "dispute-9"])
        XCTAssertEqual(vm.pickedEvidence.map(\.upload), [.uploaded, .uploaded])
        XCTAssertEqual(vm.submitState, .idle)
        XCTAssertEqual(emitted, "dispute-9")
        XCTAssertNil(snackbar.current)
        XCTAssertFalse(urls.contains(where: exists))
    }

    func testAFileIsUploadingWhileItsRequestIsInFlight() async {
        let client = FakeDisputeClient()
        client.createResult = .success("dispute-9")
        let (vm, _) = makeVM(client: client)
        vm.addEvidence(pdf(), fileName: "a.pdf")
        var uploadDuringRequest: EvidenceUploadState?
        var submitDuringRequest: ActionState?
        client.onUpload = {
            uploadDuringRequest = vm.pickedEvidence.first?.upload
            submitDuringRequest = vm.submitState
        }

        await vm.submit(reason: 3, description: validDescription)

        XCTAssertEqual(uploadDuringRequest, .uploading)
        XCTAssertEqual(submitDuringRequest, .submitting)
        XCTAssertEqual(vm.pickedEvidence.first?.upload, .uploaded)
        XCTAssertEqual(vm.submitState, .idle)
    }

    func testOneFailedUploadKeepsTheDisputeMarksThatFileNamesItAndNeverCreatesTwice() async {
        let client = FakeDisputeClient()
        client.createResult = .success("dispute-9")
        client.uploadResults = [.failure(ApiError(httpStatus: 500)), client.uploadResult]
        let snackbar = SnackbarController()
        let (vm, _) = makeVM(client: client, snackbar: snackbar)
        vm.addEvidence(pdf("a"), fileName: "a.pdf")
        vm.addEvidence(pdf("r"), fileName: "r.pdf")
        var emitted: [String] = []
        vm.created.sink { emitted.append($0) }.store(in: &cancellables)

        await vm.submit(reason: 3, description: validDescription)

        XCTAssertEqual(vm.pickedEvidence.map(\.upload), [.failed, .uploaded])
        XCTAssertEqual(snackbar.current?.text, L10n.Disputes.createEvidencePartial("a.pdf"))
        XCTAssertEqual(vm.submitState, .idle)
        XCTAssertEqual(emitted, ["dispute-9"])

        await vm.submit(reason: 3, description: validDescription)

        XCTAssertEqual(client.createCallCount, 1)
        XCTAssertEqual(client.uploadCallCount, 3)
        XCTAssertEqual(client.uploadedFiles.last, vm.pickedEvidence.first?.file.url)
        XCTAssertEqual(vm.pickedEvidence.map(\.upload), [.uploaded, .uploaded])
        XCTAssertEqual(emitted, ["dispute-9", "dispute-9"])
    }

    func testAFailedCreateUploadsNothingAndLeavesTheFilesPending() async {
        let client = FakeDisputeClient()
        client.createResult = .failure(ApiError(httpStatus: 500))
        let (vm, _) = makeVM(client: client)
        vm.addEvidence(pdf(), fileName: "a.pdf")

        await vm.submit(reason: 3, description: validDescription)

        XCTAssertEqual(client.uploadCallCount, 0)
        XCTAssertEqual(vm.pickedEvidence.first?.upload, .pending)
        XCTAssertEqual(vm.submitState, .error(L10n.Disputes.createRetryHint))
    }

    func testTheFileListIsFrozenWhileSubmitting() async {
        let client = FakeDisputeClient()
        client.createResult = .failure(ApiError(httpStatus: 500))
        let (vm, _) = makeVM(client: client)
        vm.addEvidence(pdf("a"), fileName: "a.pdf")
        client.onCreate = {
            vm.addEvidence(self.pdf("r"), fileName: "r.pdf")
            if let first = vm.pickedEvidence.first { vm.removeEvidence(id: first.id) }
        }

        await vm.submit(reason: 3, description: validDescription)

        XCTAssertEqual(vm.pickedEvidence.map(\.fileName), ["a.pdf"])
    }
}
