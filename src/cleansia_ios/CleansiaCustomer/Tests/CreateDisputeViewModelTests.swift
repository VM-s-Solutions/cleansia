import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
final class CreateDisputeViewModelTests: XCTestCase {
    func testOrderContextUsesPublicNumberAndKeepsInternalIdOnlyForSubmission() async {
        let client = FakeDisputeClient()
        let orderClient = FakeOrderClient()
        orderClient.detailResults = [.success(OrderFixtures.detail(
            id: "opaque-order-id",
            displayOrderNumber: "ORD-1042"
        ))]
        let vm = CreateDisputeViewModel(
            orderId: "opaque-order-id",
            repository: DisputeRepository(client: client, pageSize: 1),
            orderClient: orderClient,
            snackbar: SnackbarController()
        )
        await vm.loadLineOptions()
        XCTAssertEqual(vm.displayOrderNumber, "ORD-1042")
        await vm.submit(reason: 3, description: "The cleaner skipped the kitchen")
        XCTAssertEqual(client.lastCreate?.orderId, "opaque-order-id")
    }

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

    /// A card refund unless the customer explicitly chooses credit, which expires and is never paid out.
    func testAComplaintAsksForACardRefundUnlessCreditIsChosen() async {
        let client = FakeDisputeClient()
        let (vm, _) = makeVM(client: client)
        XCTAssertEqual(vm.settlement, .cardRefund)

        await vm.submit(reason: 3, description: validDescription)

        XCTAssertEqual(client.lastCreateSettlement, .cardRefund)
    }

    func testAChosenCreditTravelsWithTheComplaint() async {
        let client = FakeDisputeClient()
        let (vm, _) = makeVM(client: client)
        vm.settlement = .credit

        await vm.submit(reason: 1, description: validDescription)

        XCTAssertEqual(client.lastCreateSettlement, .credit)
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

    func testAddEvidenceKeepsAValidFileAsPending() async {
        let (vm, _) = makeVM(client: FakeDisputeClient())

        await vm.addEvidence(pdf(), fileName: "receipt.pdf")

        XCTAssertEqual(vm.pickedEvidence.map(\.fileName), ["receipt.pdf"])
        XCTAssertEqual(vm.pickedEvidence.first?.upload, .pending)
        XCTAssertEqual(vm.pickedEvidence.first?.isPdf, true)
        XCTAssertEqual(vm.pickedEvidence.first.map { exists($0.file.url) }, true)
    }

    func testAddEvidenceRefusesAnOversizedFile() async {
        let snackbar = SnackbarController()
        let (vm, _) = makeVM(client: FakeDisputeClient(), snackbar: snackbar)

        await vm.addEvidence(.pdf(Data(count: DisputeFormConstants.maxEvidenceBytes + 1)), fileName: "big.pdf")

        XCTAssertTrue(vm.pickedEvidence.isEmpty)
        XCTAssertEqual(snackbar.current?.text, L10n.Disputes.evidenceTooLarge)
    }

    /// The main actor runs the first check while a camera-size photo is still encoding, so the encode is off it.
    func testAPhotoIsEncodedOffTheMainActor() async {
        let (vm, _) = makeVM(client: FakeDisputeClient())
        let photo = ProfileFixtures.image(width: 4032, height: 3024)

        let pick = Task { await vm.addEvidence(.image(photo), fileName: "a.jpg") }
        await Task.yield()
        XCTAssertTrue(vm.pickedEvidence.isEmpty, "the encode held the main actor")

        await pick.value
        XCTAssertEqual(vm.pickedEvidence.map(\.fileName), ["a.jpg"])
    }

    /// A submit that starts while a photo is still encoding has already taken the list, so the photo is not added.
    func testAPhotoStillEncodingWhenASubmitStartsIsNotAdded() async {
        let client = FakeDisputeClient()
        client.createResult = .failure(ApiError(httpStatus: 500))
        let (vm, _) = makeVM(client: client)
        let pick = Task { await vm.addEvidence(.image(ProfileFixtures.image()), fileName: "a.jpg") }
        await Task.yield()
        client.onCreate = { await pick.value }

        await vm.submit(reason: 3, description: validDescription)

        XCTAssertTrue(vm.pickedEvidence.isEmpty)
    }

    func testRemoveEvidenceDropsOnlyThatFileAndItsTempCopy() async {
        let (vm, _) = makeVM(client: FakeDisputeClient())
        await vm.addEvidence(pdf("a"), fileName: "a.pdf")
        await vm.addEvidence(pdf("r"), fileName: "r.pdf")
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
        await vm.addEvidence(pdf("a"), fileName: "a.pdf")
        await vm.addEvidence(pdf("r"), fileName: "r.pdf")
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
        await vm.addEvidence(pdf(), fileName: "a.pdf")
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
        await vm.addEvidence(pdf("a"), fileName: "a.pdf")
        await vm.addEvidence(pdf("r"), fileName: "r.pdf")
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
        await vm.addEvidence(pdf(), fileName: "a.pdf")

        await vm.submit(reason: 3, description: validDescription)

        XCTAssertEqual(client.uploadCallCount, 0)
        XCTAssertEqual(vm.pickedEvidence.first?.upload, .pending)
        XCTAssertEqual(vm.submitState, .error(L10n.Disputes.createRetryHint))
    }

    func testTheFileListIsFrozenWhileSubmitting() async {
        let client = FakeDisputeClient()
        client.createResult = .failure(ApiError(httpStatus: 500))
        let (vm, _) = makeVM(client: client)
        await vm.addEvidence(pdf("a"), fileName: "a.pdf")
        client.onCreate = {
            await vm.addEvidence(self.pdf("r"), fileName: "r.pdf")
            if let first = vm.pickedEvidence.first { vm.removeEvidence(id: first.id) }
        }

        await vm.submit(reason: 3, description: validDescription)

        XCTAssertEqual(vm.pickedEvidence.map(\.fileName), ["a.pdf"])
    }
}
