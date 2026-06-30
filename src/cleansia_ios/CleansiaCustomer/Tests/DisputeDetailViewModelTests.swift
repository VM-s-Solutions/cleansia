import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
final class DisputeDetailViewModelTests: XCTestCase {
    private var cancellables: Set<AnyCancellable> = []

    private func makeVM(
        disputeId: String = "dispute-1",
        client: FakeDisputeClient
    ) -> (DisputeDetailViewModel, DisputeRepository) {
        let repo = DisputeRepository(client: client, pageSize: 1)
        let vm = DisputeDetailViewModel(disputeId: disputeId, repository: repo, snackbar: SnackbarController())
        return (vm, repo)
    }

    private func smallPdf() -> Data {
        Data("%PDF-1.4 minimal".utf8)
    }

    // MARK: - Load

    func testLoadSuccessSurfacesLoaded() async {
        let client = FakeDisputeClient()
        client.detailResults = [.success(DisputeFixtures.detail())]
        let (vm, _) = makeVM(client: client)

        await vm.load()

        XCTAssertEqual(vm.state.loadedValue?.id, "dispute-1")
    }

    func testLoadFailureFlipsToError() async {
        let client = FakeDisputeClient()
        client.detailResults = [.failure(ApiError(httpStatus: 500))]
        let (vm, _) = makeVM(client: client)

        await vm.load()

        if case .error = vm.state {} else { XCTFail("expected error state") }
    }

    func testBlankDisputeIdFlipsToError() async {
        let client = FakeDisputeClient()
        let (vm, _) = makeVM(disputeId: "  ", client: client)

        await vm.load()

        if case .error = vm.state {} else { XCTFail("expected error state") }
        XCTAssertEqual(client.detailCallCount, 0)
    }

    // MARK: - Reply gate

    func testReplyGateAllowsOnLiveStatuses() {
        XCTAssertTrue(DisputeFixtures.detail(statusValue: 1).allowsMessages)
        XCTAssertTrue(DisputeFixtures.detail(statusValue: 3).allowsMessages)
    }

    func testReplyGateBlocksOnTerminalStatuses() {
        XCTAssertFalse(DisputeFixtures.detail(statusValue: 4).allowsMessages)
        XCTAssertFalse(DisputeFixtures.detail(statusValue: 5).allowsMessages)
        XCTAssertFalse(DisputeFixtures.detail(statusValue: 6).allowsMessages)
    }

    // MARK: - sendMessage

    func testSendMessageSuccessEmitsEffectReloadsAndReturnsIdle() async {
        let client = FakeDisputeClient()
        client.detailResults = [.success(DisputeFixtures.detail())]
        let (vm, _) = makeVM(client: client)
        await vm.load()

        var sent = false
        vm.messageSent.sink { sent = true }.store(in: &cancellables)

        await vm.sendMessage("hello there")

        XCTAssertTrue(sent)
        XCTAssertEqual(vm.sendState, .idle)
        XCTAssertEqual(client.lastMessage, "hello there")
        XCTAssertGreaterThanOrEqual(client.detailCallCount, 2) // reload after send
    }

    func testSendMessageTrimsAndRejectsWhitespaceOnly() async {
        let client = FakeDisputeClient()
        client.detailResults = [.success(DisputeFixtures.detail())]
        let (vm, _) = makeVM(client: client)
        await vm.load()

        await vm.sendMessage("   ")

        XCTAssertEqual(client.addMessageCallCount, 0)
    }

    func testSendMessageFailureSurfacesError() async {
        let client = FakeDisputeClient()
        client.detailResults = [.success(DisputeFixtures.detail())]
        client.addMessageResult = .failure(ApiError(httpStatus: 500))
        let (vm, _) = makeVM(client: client)
        await vm.load()

        await vm.sendMessage("hello there")

        if case .error = vm.sendState {} else { XCTFail("expected send error") }
    }

    // MARK: - uploadEvidence (PDF path — no UIKit needed)

    func testUploadEvidenceSuccessEmitsEffectReloadsReturnsIdle() async {
        let client = FakeDisputeClient()
        client.detailResults = [.success(DisputeFixtures.detail())]
        let (vm, _) = makeVM(client: client)
        await vm.load()

        var uploaded = false
        vm.evidenceUploaded.sink { uploaded = true }.store(in: &cancellables)

        await vm.uploadEvidence([.pdf(smallPdf())])

        XCTAssertTrue(uploaded)
        XCTAssertEqual(vm.uploadState, .idle)
        XCTAssertEqual(client.uploadCallCount, 1)
    }

    func testUploadEvidenceUploadsTempPdfWithCorrectExtensionAndCleansUp() async {
        let client = FakeDisputeClient()
        client.detailResults = [.success(DisputeFixtures.detail())]
        let (vm, _) = makeVM(client: client)
        await vm.load()

        await vm.uploadEvidence([.pdf(smallPdf())])

        let uploaded = try? XCTUnwrap(client.uploadedFiles.first)
        XCTAssertEqual(uploaded?.pathExtension, "pdf")
        if let uploaded {
            XCTAssertFalse(FileManager.default.fileExists(atPath: uploaded.path)) // cleaned up
        }
    }

    func testUploadEvidenceRejectsOversizePdfBeforeCall() async {
        let client = FakeDisputeClient()
        client.detailResults = [.success(DisputeFixtures.detail())]
        let (vm, _) = makeVM(client: client)
        await vm.load()

        let tooBig = Data(count: DisputeFormConstants.maxEvidenceBytes + 1)
        await vm.uploadEvidence([.pdf(tooBig)])

        XCTAssertEqual(client.uploadCallCount, 0)
        XCTAssertEqual(vm.uploadState, .idle)
    }

    func testUploadEvidenceFiresPerFileSequentially() async {
        let client = FakeDisputeClient()
        client.detailResults = [.success(DisputeFixtures.detail())]
        let (vm, _) = makeVM(client: client)
        await vm.load()

        await vm.uploadEvidence([.pdf(smallPdf()), .pdf(smallPdf()), .pdf(smallPdf())])

        XCTAssertEqual(client.uploadCallCount, 3)
        XCTAssertEqual(client.uploadedFiles.map(\.pathExtension), ["pdf", "pdf", "pdf"])
    }

    func testUploadEvidenceMixedValidAndInvalidUploadsOnlyValid() async {
        let client = FakeDisputeClient()
        client.detailResults = [.success(DisputeFixtures.detail())]
        let (vm, _) = makeVM(client: client)
        await vm.load()

        let tooBig = Data(count: DisputeFormConstants.maxEvidenceBytes + 1)
        await vm.uploadEvidence([.pdf(tooBig), .pdf(smallPdf())])

        XCTAssertEqual(client.uploadCallCount, 1)
    }
}
