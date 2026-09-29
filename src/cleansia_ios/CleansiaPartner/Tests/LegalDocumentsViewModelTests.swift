import CleansiaCore
import CleansiaPartnerApi
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
final class LegalDocumentsViewModelTests: XCTestCase {
    private var client: FakePartnerProfileClient!
    private var snackbar: SnackbarController!
    private var cancellables: Set<AnyCancellable>!
    private var accepted: [LegalDocumentType]!

    override func setUp() {
        super.setUp()
        client = FakePartnerProfileClient()
        snackbar = SnackbarController()
        cancellables = []
        accepted = []
    }

    private func makeVM(language: String = "cs") -> LegalDocumentsViewModel {
        let vm = LegalDocumentsViewModel(client: client, snackbar: snackbar, languageTag: { language })
        vm.accepted.sink { [weak self] in self?.accepted.append($0) }.store(in: &cancellables)
        return vm
    }

    func testOpeningReadsTheDocumentsInTheReadersLanguage() async {
        client.legalDocumentsResult = .success([.sample(type: ._3), .sample(type: ._5, textId: "text-2")])
        let vm = makeVM(language: "sk")
        XCTAssertTrue(vm.state.isLoading)

        await vm.load()

        XCTAssertEqual(client.legalDocumentLanguages, ["sk"])
        XCTAssertEqual(vm.state.loadedValue?.map(\.type), [._3, ._5])
    }

    func testNothingInForceIsAnEmptyListNotAnError() async {
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(vm.state.loadedValue, [])
    }

    func testAFailedFirstReadIsTheErrorStateAndRetryReadsAgain() async {
        client.legalDocumentsResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()
        guard case .error = vm.state else { return XCTFail("expected the error state") }

        client.legalDocumentsResult = .success([.sample(type: ._3)])
        await vm.load()

        XCTAssertEqual(client.legalDocumentLanguages.count, 2)
        XCTAssertEqual(vm.state.loadedValue?.count, 1)
    }

    func testAcceptingEchoesTheTextReadConfirmsClosesTheDocumentAndReReadsTheList() async {
        client.legalDocumentsResult = .success([.sample(type: ._3, textId: "text-cs-3")])
        let vm = makeVM()
        await vm.load()
        client.onAcceptLegalDocument = { [weak self] _ in
            self?.client.legalDocumentsResult = .success([.sample(type: ._3, textId: "text-cs-3", isAccepted: true)])
        }

        await vm.accept(.sample(type: ._3, textId: "text-cs-3"))

        XCTAssertEqual(client.acceptedLegalDocumentTextIds, ["text-cs-3"])
        XCTAssertEqual(accepted, [._3])
        XCTAssertEqual(snackbar.current?.severity, .success)
        XCTAssertEqual(snackbar.current?.text, L10n.LegalDocuments.acceptedToast)
        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertEqual(vm.state.loadedValue?.first?.isAccepted, true)
    }

    func testASecondAcceptWhileOneIsInFlightIsRefused() async {
        client.legalDocumentsResult = .success([.sample(type: ._3)])
        client.suspendAccept = true
        let vm = makeVM()
        await vm.load()

        let first = Task { await vm.accept(.sample(type: ._3)) }
        while client.acceptedLegalDocumentTextIds.isEmpty {
            await Task.yield()
        }
        XCTAssertTrue(vm.actionState.isSubmitting)
        await vm.accept(.sample(type: ._3))
        XCTAssertEqual(client.acceptedLegalDocumentTextIds.count, 1)

        client.resumeAccept()
        await first.value
        XCTAssertEqual(vm.actionState, .idle)
    }

    func testATextNoLongerInForceReReadsTheListShowsTheNoticeAndKeepsTheDocumentOpen() async {
        client.legalDocumentsResult = .success([.sample(type: ._3, textId: "text-old")])
        let vm = makeVM()
        await vm.load()
        client.acceptLegalDocumentResult = .failure(ApiError(code: "legal.document_not_in_force", httpStatus: 400))
        client.legalDocumentsResult = .success([.sample(type: ._3, textId: "text-new", version: "2027-01-01")])

        await vm.accept(.sample(type: ._3, textId: "text-old"))

        XCTAssertEqual(vm.notice, .textUpdated)
        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertTrue(accepted.isEmpty, "the open document stays open on the new text")
        XCTAssertEqual(vm.document(ofType: ._3)?.legalDocumentTextId, "text-new")
    }

    func testAnyOtherRefusalIsShownOnTheDocumentAndNothingIsReRead() async {
        client.legalDocumentsResult = .success([.sample(type: ._3)])
        let vm = makeVM()
        await vm.load()
        client.acceptLegalDocumentResult = .failure(ApiError(code: "network.unreachable"))

        await vm.accept(.sample(type: ._3))

        XCTAssertNotNil(vm.actionState.errorMessage)
        XCTAssertNil(vm.notice)
        XCTAssertEqual(client.legalDocumentLanguages.count, 1)
        XCTAssertTrue(accepted.isEmpty)
    }

    func testClosingTheDocumentClearsWhatItLeftBehind() async {
        client.legalDocumentsResult = .success([.sample(type: ._3)])
        let vm = makeVM()
        await vm.load()
        client.acceptLegalDocumentResult = .failure(ApiError(code: "legal.document_not_in_force", httpStatus: 400))
        await vm.accept(.sample(type: ._3))
        client.acceptLegalDocumentResult = .failure(ApiError(code: "network.unreachable"))
        await vm.accept(.sample(type: ._3))

        vm.onDocumentClosed()

        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertNil(vm.notice)
    }

    func testAFailedReReadKeepsTheListOnScreen() async {
        client.legalDocumentsResult = .success([.sample(type: ._3)])
        let vm = makeVM()
        await vm.load()

        client.legalDocumentsResult = .failure(ApiError(code: "network.unreachable"))
        await vm.load()

        XCTAssertEqual(vm.state.loadedValue?.count, 1)
        XCTAssertEqual(snackbar.current?.severity, .error)
    }
}
