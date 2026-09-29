import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

/// What the customer owes, paid through a pay link, and the card that guarantees their cash bookings.
@MainActor
final class PaymentsViewModelTests: XCTestCase {
    private var cards: FakeSavedCardClient!
    private var receivables: FakeReceivableClient!
    private var snackbar: SnackbarController!
    private var cancellables = Set<AnyCancellable>()

    override func setUp() {
        super.setUp()
        cards = FakeSavedCardClient(reads: [.success([PaymentsFixtures.czkCard])])
        receivables = FakeReceivableClient(receivablesResult: .success([PaymentsFixtures.receivable()]))
        snackbar = SnackbarController()
    }

    private func makeVM() -> PaymentsViewModel {
        PaymentsViewModel(savedCardClient: cards, receivableClient: receivables, snackbar: snackbar)
    }

    func testLoadShowsWhatIsOwedAndTheCard() async {
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(vm.state.loadedValue, PaymentsSnapshot(
            receivables: [PaymentsFixtures.receivable()],
            cards: [PaymentsFixtures.czkCard]
        ))
    }

    /// Half a page would read as "nothing owed" or "no card" while the server says otherwise.
    func testEitherReadFailingIsAnErrorNotHalfAPage() async {
        receivables.receivablesResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()

        await vm.load()

        guard case .error = vm.state else { return XCTFail("a failed receivables read rendered a page") }
        XCTAssertNotNil(snackbar.current)

        receivables.receivablesResult = .success([])
        cards.reads = [.failure(ApiError(httpStatus: 500))]
        await vm.load()

        guard case .error = vm.state else { return XCTFail("a failed cards read rendered a page") }
    }

    func testPayingOpensTheCheckoutPageAndReReadsOnReturn() async {
        let vm = makeVM()
        await vm.load()
        var opened: [URL] = []
        vm.payLinks.sink { opened.append($0) }.store(in: &cancellables)

        await vm.pay(PaymentsFixtures.receivable())

        XCTAssertEqual(receivables.payLinkIds, ["rcv-1"])
        XCTAssertEqual(opened, [PaymentsFixtures.checkoutUrl])

        receivables.receivablesResult = .success([])
        await vm.onResumed()
        XCTAssertEqual(vm.state.loadedValue?.receivables, [], "the paid amount is still shown on return")

        receivables.receivablesResult = .success([PaymentsFixtures.receivable(id: "rcv-2")])
        await vm.onResumed()
        XCTAssertEqual(vm.state.loadedValue?.receivables, [], "a return with no pay link opened re-read the page")
    }

    func testAPayLinkTheServerRefusesOpensNothing() async {
        receivables.payLinkResult = .failure(ApiError(code: "receivable.not_open", httpStatus: 400))
        let vm = makeVM()
        await vm.load()
        var opened: [URL] = []
        vm.payLinks.sink { opened.append($0) }.store(in: &cancellables)

        await vm.pay(PaymentsFixtures.receivable())

        XCTAssertEqual(opened, [])
        XCTAssertNotNil(snackbar.current)
        XCTAssertFalse(vm.payState.isSubmitting)
    }

    func testRemovingTheCardTakesItOffThePage() async {
        let vm = makeVM()
        await vm.load()
        var removed: [String] = []
        vm.removed.sink { removed.append($0) }.store(in: &cancellables)

        await vm.remove(PaymentsFixtures.czkCard)

        XCTAssertEqual(cards.removedIds, ["card-czk"])
        XCTAssertEqual(removed, ["card-czk"])
        XCTAssertEqual(vm.state.loadedValue?.cards, [])
        XCTAssertEqual(vm.state.loadedValue?.receivables, [PaymentsFixtures.receivable()])
        XCTAssertEqual(vm.removeState, .idle)
    }

    func testARefusedRemovalKeepsTheCardAndSaysSo() async {
        cards.removeResult = .failure(ApiError(code: "saved_card.not_found", httpStatus: 400))
        let vm = makeVM()
        await vm.load()

        await vm.remove(PaymentsFixtures.czkCard)

        XCTAssertEqual(vm.state.loadedValue?.cards, [PaymentsFixtures.czkCard])
        XCTAssertEqual(vm.removeState, .error(L10n.Payments.cardRemoveRetryHint))
        XCTAssertNotNil(snackbar.current)
    }

    func testEveryKindTheServerSendsHasItsOwnLabelAndANewOneReadsAsAmountDue() {
        XCTAssertEqual(Receivable.Kind(wireValue: 1), .cancellationFee)
        XCTAssertEqual(Receivable.Kind(wireValue: 2), .lockout)
        XCTAssertEqual(Receivable.Kind(wireValue: 3), .unpaidCash)
        XCTAssertEqual(Receivable.Kind(wireValue: 4), .topUp)
        XCTAssertEqual(Receivable.Kind(wireValue: 9), .other)

        let labels = [Receivable.Kind.cancellationFee, .lockout, .unpaidCash, .topUp, .other].map(L10n.Payments.kind)
        XCTAssertEqual(Set(labels).count, labels.count, "two kinds read the same")
        XCTAssertFalse(labels.contains { $0.hasPrefix("receivable_kind_") }, "a kind shows its raw key")
    }
}
