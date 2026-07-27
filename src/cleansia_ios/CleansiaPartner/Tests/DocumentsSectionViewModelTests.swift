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

    func testDeleteSuccessClearsDeletingId() async {
        client.documentsResult = .success([
            GetMyDocumentsMyDocumentDto(documentId: "doc-1", fileName: "passport.pdf")
        ])
        let vm = makeVM()
        await vm.load()
        await vm.delete(documentId: "doc-1")
        XCTAssertNil(vm.deletingId)
        XCTAssertEqual(client.deletedDocumentId, "doc-1")
    }

    func testDeleteFailureSnackbarsAndClearsDeletingId() async {
        client.documentsResult = .success([
            GetMyDocumentsMyDocumentDto(documentId: "doc-1", fileName: "passport.pdf")
        ])
        client.deleteDocumentResult = .failure(ApiError(httpStatus: 400))
        let vm = makeVM()
        await vm.load()
        await vm.delete(documentId: "doc-1")
        XCTAssertNil(vm.deletingId)
        XCTAssertNotNil(snackbar.current)
    }
}
