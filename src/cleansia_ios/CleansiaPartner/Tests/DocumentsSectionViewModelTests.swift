import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class DocumentsSectionViewModelTests: XCTestCase {
    private var client: FakePartnerProfileClient!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerProfileClient()
        snackbar = SnackbarController()
    }

    private func makeVM() -> DocumentsSectionViewModel {
        DocumentsSectionViewModel(client: client, snackbar: snackbar)
    }

    func testLoadSuccessMapsDocuments() async {
        client.documentsResult = .success([
            GetMyDocumentsMyDocumentDto(documentId: "doc-1", fileName: "passport.pdf")
        ])
        let vm = makeVM()
        await vm.load()
        guard case let .loaded(docs) = vm.state else { return XCTFail("expected loaded") }
        XCTAssertEqual(docs.count, 1)
    }

    func testLoadFailureSetsErrorAndSnackbars() async {
        client.documentsResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()
        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    func testUploadSuccessReloads() async {
        client.documentsResult = .success([])
        let vm = makeVM()
        await vm.load()
        await vm.upload(
            documentType: ._1,
            fileName: "passport.pdf",
            contentType: "application/pdf",
            base64Content: "AAA",
            description: nil
        )
        XCTAssertEqual(vm.action, .idle)
        XCTAssertEqual(client.saveDocumentsCommand?.documents?.first?.file?.fileName, "passport.pdf")
    }

    /// Regression guard for the bug this replaced: every upload used to be
    /// hardcoded to `._1` (IdentityCard) with a nil description, so admin
    /// verification saw the wrong type on every document.
    func testUploadSendsTheChosenTypeAndTrimmedDescription() async {
        client.documentsResult = .success([])
        let vm = makeVM()
        await vm.load()
        await vm.upload(
            documentType: ._4,
            fileName: "permit.pdf",
            contentType: "application/pdf",
            base64Content: "AAA",
            description: "  work permit 2026  "
        )
        let saved = client.saveDocumentsCommand?.documents?.first
        XCTAssertEqual(saved?.documentType, ._4)
        XCTAssertEqual(saved?.description, "work permit 2026")
    }

    func testUploadSendsNilForABlankDescription() async {
        client.documentsResult = .success([])
        let vm = makeVM()
        await vm.load()
        await vm.upload(
            documentType: ._10,
            fileName: "scan.pdf",
            contentType: "application/pdf",
            base64Content: "AAA",
            description: "   "
        )
        XCTAssertNil(client.saveDocumentsCommand?.documents?.first?.description)
    }

    func testShowTooLargeSnackbars() {
        let vm = makeVM()
        vm.showTooLarge()
        XCTAssertNotNil(snackbar.current)
    }

    /// The whole design in one assertion. Asking removes NOTHING, so the list is deliberately NOT
    /// reloaded — a screen that looked different afterwards would be lying about what just happened.
    /// The button this replaced soft-deleted on the spot, and that flipped `AreDocumentsUploaded` and
    /// re-engaged the registration lock: one tap, no dialog, no access to work.
    func testRequestingDeletionSendsTheReasonAndLeavesTheListAlone() async {
        client.documentsResult = .success([
            GetMyDocumentsMyDocumentDto(documentId: "doc-1", fileName: "passport.pdf")
        ])
        let vm = makeVM()
        await vm.load()
        await vm.requestDeletion(documentId: "doc-1", reason: "Wrong file")
        XCTAssertNil(vm.busyDocumentId)
        XCTAssertEqual(client.deletionRequestedFor, "doc-1")
        XCTAssertEqual(client.deletionReason, "Wrong file")
        XCTAssertEqual(vm.state.loadedValue?.count, 1)
    }

    func testRequestDeletionFailureSnackbarsAndClearsBusyId() async {
        client.documentsResult = .success([
            GetMyDocumentsMyDocumentDto(documentId: "doc-1", fileName: "passport.pdf")
        ])
        client.requestDeletionResult = .failure(ApiError(httpStatus: 400))
        let vm = makeVM()
        await vm.load()
        await vm.requestDeletion(documentId: "doc-1", reason: "Wrong file")
        XCTAssertNil(vm.busyDocumentId)
        XCTAssertNotNil(snackbar.current)
    }

    /// Replacing carries the file and NOT a document type: the server takes the type from the version
    /// being replaced, so a replacement cannot relabel a document an admin already approved.
    func testReplaceSendsTheFileAgainstTheTargetDocument() async {
        client.documentsResult = .success([
            GetMyDocumentsMyDocumentDto(documentId: "doc-1", fileName: "passport.pdf")
        ])
        let vm = makeVM()
        await vm.load()
        await vm.replace(
            documentId: "doc-1",
            fileName: "passport-new.jpg",
            contentType: "image/jpeg",
            base64Content: "AAAA",
            description: nil
        )
        XCTAssertEqual(client.replacedDocumentId, "doc-1")
        XCTAssertEqual(client.replacedFile?.fileName, "passport-new.jpg")
    }

    /// The checklist is advisory, so a failure loading it must not cost the cleaner the documents they
    /// CAN see. It falls back to the empty state the screen had before the section existed.
    func testRequirementsFailureLeavesTheDocumentListLoaded() async {
        client.documentsResult = .success([
            GetMyDocumentsMyDocumentDto(documentId: "doc-1", fileName: "passport.pdf")
        ])
        client.requirementsResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()
        XCTAssertEqual(vm.state.loadedValue?.count, 1)
        XCTAssertTrue(vm.requirements.isEmpty)
    }

    /// A checklist in arbitrary order reads as arbitrary — the server orders these and so does this.
    func testRequirementsAreOrderedBySortOrder() async {
        client.requirementsResult = .success([
            MyDocumentRequirementDto(documentType: ._4, isRequired: false, sortOrder: 2),
            MyDocumentRequirementDto(documentType: ._1, isRequired: true, sortOrder: 1)
        ])
        let vm = makeVM()
        await vm.load()
        XCTAssertEqual(vm.requirements.map(\.documentType), [._1, ._4])
    }
}
