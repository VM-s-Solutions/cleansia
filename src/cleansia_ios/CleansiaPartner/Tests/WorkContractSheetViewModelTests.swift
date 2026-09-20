import CleansiaCore
import CleansiaPartnerApi
import Combine
import XCTest
@testable import CleansiaPartner

/// The sheet is where the take now happens: it renders the text the server chose for this order and
/// echoes exactly that text row on the swipe. The two contract verdicts are its own — the mismatch
/// re-runs the preview under a notice, the missing document is the unavailable state — and every other
/// verdict leaves the sheet as an outcome its host reconciles the way it always reconciled a take.
@MainActor
final class WorkContractSheetViewModelTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var cancellables: Set<AnyCancellable>!
    private var outcomes: [WorkContractOutcome]!

    override func setUp() {
        super.setUp()
        client = FakePartnerOrderClient()
        cancellables = []
        outcomes = []
    }

    private func makeVM(_ request: WorkContractRequest, language: String = "cs") -> WorkContractSheetViewModel {
        let vm = WorkContractSheetViewModel(request: request, client: client, languageTag: { language })
        vm.outcome.sink { [weak self] in self?.outcomes.append($0) }.store(in: &cancellables)
        return vm
    }

    // MARK: loading

    func testATakeLoadsThePreviewForItsOrderInTheReadersLanguage() async {
        let vm = makeVM(.take(orderId: "order-1"), language: "uk")
        guard case .loading = vm.state else { return XCTFail("expected loading before the first load") }

        await vm.load()

        XCTAssertEqual(client.previewRequests.map(\.orderId), ["order-1"])
        XCTAssertEqual(client.previewRequests.map(\.language), ["uk"])
        XCTAssertTrue(client.contractRequests.isEmpty)
        XCTAssertEqual(vm.state.loadedContract?.legalDocumentTextId, "text-1")
        XCTAssertTrue(vm.hasGesture)
    }

    func testAnAcceptLoadsTheSamePreviewAsATake() async {
        let vm = makeVM(.accept(orderId: "order-1"))

        await vm.load()

        XCTAssertEqual(client.previewRequests.map(\.orderId), ["order-1"])
        XCTAssertTrue(vm.hasGesture)
    }

    func testAReadLoadsTheAcceptedContractByItsAcceptanceAndHasNothingToSwipe() async {
        let vm = makeVM(.read(acceptanceId: "acc-9"))

        await vm.load()

        XCTAssertEqual(client.contractRequests.map(\.acceptanceId), ["acc-9"])
        XCTAssertTrue(client.previewRequests.isEmpty)
        XCTAssertNotNil(vm.state.loadedContract?.acceptance)
        XCTAssertFalse(vm.hasGesture)
    }

    func testAFailedLoadIsTheErrorState() async {
        client.previewResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM(.take(orderId: "order-1"))

        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected the error state") }
    }

    /// The one preview refusal with its own screen: an order booked under no contract text cannot be
    /// taken now, and there is nothing to retry.
    func testAMissingDocumentIsTheUnavailableStateNotAnError() async {
        client.previewResult = .failure(ApiError(code: WorkContractErrorKey.documentNotFound, httpStatus: 400))
        let vm = makeVM(.take(orderId: "order-1"))

        await vm.load()

        guard case let .unavailable(error) = vm.state else { return XCTFail("expected the unavailable state") }
        XCTAssertEqual(error.code, WorkContractErrorKey.documentNotFound)
    }

    // MARK: the swipe

    func testTheSwipeTakesTheOrderOnceWithThePreviewedTextId() async {
        client.previewResult = .success(.sample(textId: "text-cs-7"))
        let vm = makeVM(.take(orderId: "order-1"))
        await vm.load()

        await vm.accept()

        XCTAssertEqual(client.commands.map(\.name), ["take"])
        XCTAssertEqual(client.commands.map(\.orderId), ["order-1"])
        XCTAssertEqual(client.echoedTextIds, ["text-cs-7"])
        XCTAssertEqual(outcomes, [.taken(orderId: "order-1")])
        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertNil(vm.notice)
    }

    func testTheSwipeInAcceptModeCallsTheStandaloneAcceptanceWithTheSameEcho() async {
        client.previewResult = .success(.sample(textId: "text-cs-7"))
        let vm = makeVM(.accept(orderId: "order-1"))
        await vm.load()

        await vm.accept()

        XCTAssertEqual(client.commands.map(\.name), ["acceptWorkContract"])
        XCTAssertEqual(client.echoedTextIds, ["text-cs-7"])
        XCTAssertEqual(outcomes, [.accepted(orderId: "order-1")])
    }

    func testAReadNeverSubmitsAnything() async {
        let vm = makeVM(.read(acceptanceId: "acc-9"))
        await vm.load()

        await vm.accept()

        XCTAssertTrue(client.commands.isEmpty)
        XCTAssertTrue(outcomes.isEmpty)
    }

    func testNothingIsSubmittedBeforeTheContractIsOnScreen() async {
        let vm = makeVM(.take(orderId: "order-1"))

        await vm.accept()

        XCTAssertTrue(client.commands.isEmpty)
        XCTAssertTrue(outcomes.isEmpty)
    }

    /// The echoed text is not this order's: the sheet re-runs the preview, ends the busy state so the
    /// thumb springs back, and says why — the host never hears about it.
    func testAMismatchReloadsThePreviewResetsTheGestureAndShowsTheNotice() async {
        let vm = makeVM(.take(orderId: "order-1"))
        await vm.load()
        client.commandResult = .failure(ApiError(code: WorkContractErrorKey.textMismatch, httpStatus: 400))
        client.previewResult = .success(.sample(textId: "text-cs-8"))

        await vm.accept()

        XCTAssertEqual(client.previewRequests.count, 2)
        XCTAssertEqual(vm.state.loadedContract?.legalDocumentTextId, "text-cs-8")
        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertEqual(vm.notice, .textUpdated)
        XCTAssertTrue(outcomes.isEmpty)
    }

    func testTheSecondSwipeAfterAMismatchEchoesTheReloadedText() async {
        let vm = makeVM(.take(orderId: "order-1"))
        await vm.load()
        client.commandResult = .failure(ApiError(code: WorkContractErrorKey.textMismatch, httpStatus: 400))
        client.previewResult = .success(.sample(textId: "text-cs-8"))
        await vm.accept()
        client.commandResult = .success(())

        await vm.accept()

        XCTAssertEqual(client.echoedTextIds, ["text-1", "text-cs-8"])
        XCTAssertEqual(outcomes, [.taken(orderId: "order-1")])
    }

    func testTheNoticeReadsAsASentenceNotTheRawKey() {
        let message = WorkContractNotice.textUpdated.message

        XCTAssertNotEqual(message, WorkContractErrorKey.textMismatch)
        XCTAssertFalse(message.isEmpty)
    }

    /// Every other refusal — a seat race, the weekly cap, a time conflict — is the host's to frame in
    /// its own words, so it leaves the sheet untouched.
    func testAnyOtherRefusalIsHandedToTheHostAsTheOutcome() async {
        let vm = makeVM(.take(orderId: "order-1"))
        await vm.load()
        let refusal = ApiError(code: "order.no_available_spots", httpStatus: 400)
        client.commandResult = .failure(refusal)

        await vm.accept()

        XCTAssertEqual(outcomes, [.refused(.take(orderId: "order-1"), refusal)])
        XCTAssertEqual(client.previewRequests.count, 1)
        XCTAssertNil(vm.notice)
        XCTAssertEqual(vm.actionState, .idle)
    }

    func testTheOutcomeCarriesTheRequestAndTheResultTheHostReconciles() {
        let refusal = ApiError(code: "order.weekly_limit_reached", httpStatus: 400)

        XCTAssertEqual(WorkContractOutcome.taken(orderId: "o").request, .take(orderId: "o"))
        XCTAssertEqual(WorkContractOutcome.accepted(orderId: "o").request, .accept(orderId: "o"))
        XCTAssertEqual(WorkContractOutcome.refused(.take(orderId: "o"), refusal).request, .take(orderId: "o"))
        XCTAssertNil(WorkContractOutcome.taken(orderId: "o").result.apiErrorOrNil)
        XCTAssertEqual(WorkContractOutcome.refused(.take(orderId: "o"), refusal).result.apiErrorOrNil, refusal)
    }

    func testASecondSwipeWhileOneIsInFlightIsDropped() async {
        let vm = makeVM(.take(orderId: "order-1"))
        await vm.load()
        client.suspendCommands = true

        let first = Task { await vm.accept() }
        while client.commands.isEmpty {
            await Task.yield()
        }
        XCTAssertTrue(vm.actionState.isSubmitting)
        await vm.accept()

        XCTAssertEqual(client.commands.count, 1)
        client.resumeCommand()
        await first.value
        XCTAssertEqual(outcomes.count, 1)
    }
}
