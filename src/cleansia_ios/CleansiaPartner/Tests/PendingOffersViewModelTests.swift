import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation
import XCTest
@testable import CleansiaPartner

/// "Confirming IS taking" — there is no confirm endpoint, and a UI that called anything else would be a
/// second acquisition path beside `TakeOrder`'s single ordered chain. The take itself now runs inside
/// the contract sheet, so a confirm opens the sheet and its verdict comes back as an outcome. The
/// refusal cases matter as much as the happy one: a reservation spends no capacity, so a capped cleaner
/// can be reserved a job and then refused the confirm, and that refusal has to read as the platform's
/// problem.
@MainActor
final class PendingOffersViewModelTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var ordersStaleness: OrdersStaleness!
    private var snackbar: SnackbarController!
    private var store: PendingOffersStore!
    private var cancellables: Set<AnyCancellable>!

    private let weeklyCapKey = "order.weekly_limit_reached"

    override func setUp() async throws {
        client = FakePartnerOrderClient()
        ordersStaleness = OrdersStaleness()
        snackbar = SnackbarController()
        store = PendingOffersStore(client: client, ordersStaleness: ordersStaleness)
        cancellables = []
    }

    private func makeVM() -> PendingOffersViewModel {
        PendingOffersViewModel(store: store, staleness: ordersStaleness, snackbar: snackbar)
    }

    private func rows(_ state: UiState<[PendingOfferItem]>) -> [String] {
        (state.loadedValue ?? []).map { $0.id ?? "" }
    }

    private func isError(_ state: UiState<[PendingOfferItem]>) -> Bool {
        if case .error = state { return true }
        return false
    }

    /// The sheet took the seat, or was refused: the offers list hears it as the confirm's verdict.
    private func confirmed(_ vm: PendingOffersViewModel, _ id: String) async {
        vm.confirm(.sample(id: id))
        await vm.onWorkContractOutcome(.taken(orderId: id))
    }

    private func confirmRefused(_ vm: PendingOffersViewModel, _ id: String, _ key: String) async {
        vm.confirm(.sample(id: id))
        await vm.onWorkContractOutcome(.refused(.take(orderId: id), ApiError(code: key, httpStatus: 400)))
    }

    func testTheListRendersExactlyWhatTheServerSentCoarseAddressIncluded() async {
        let row = PendingOfferItem.sample(id: "a")
        client.pendingOffersResult = .success([row])
        let vm = makeVM()
        XCTAssertTrue(vm.state.isLoading)

        await vm.load()

        XCTAssertEqual(rows(vm.state), ["a"])
        let rendered = vm.state.loadedValue?.first
        XCTAssertEqual(rendered?.customerAddressApproximate, "Praha 4 · 14000")
        XCTAssertEqual(rendered?.respondByUtc, row.respondByUtc)
        XCTAssertEqual(rendered?.displayOrderNumber, "CL-a")
        XCTAssertEqual(rendered, row)
    }

    /// The cleaner has not accepted yet, so the row carries the coarse city-and-partial-postcode the
    /// pre-acceptance board already shows and nothing finer. A regenerated client that widened the DTO
    /// would put a street address or a customer's name on a screen that may never be accepted.
    func testTheOfferCarriesNoIdentityAndNoPreciseLocation() {
        let surface = Set(PendingOfferItem.CodingKeys.allCases.map(\.rawValue))

        XCTAssertEqual(
            surface,
            [
                "id",
                "displayOrderNumber",
                "cleaningDateTime",
                "estimatedTime",
                "respondByUtc",
                "customerAddressApproximate",
                "rooms",
                "bathrooms",
                "totalPrice",
                "currencyCode"
            ]
        )
    }

    func testNoOffersIsALoadedEmptyListNeverAnError() async {
        client.pendingOffersResult = .success([])
        let vm = makeVM()

        await vm.load()

        XCTAssertFalse(isError(vm.state))
        XCTAssertEqual(vm.state.loadedValue?.isEmpty, true)
    }

    func testAFirstLoadThatFailsWithNothingCachedIsTheErrorState() async {
        client.pendingOffersResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()

        await vm.load()

        XCTAssertTrue(isError(vm.state))
    }

    func testDecliningCallsTheDeclineEndpointAndTheOfferLeavesTheList() async {
        client.pendingOffersResult = .success([.sample(id: "keep"), .sample(id: "refuse")])
        let vm = makeVM()
        await vm.load()
        client.onDeclinePreferredOffer = { [weak self] _ in
            self?.client.pendingOffersResult = .success([.sample(id: "keep")])
        }

        await vm.decline(.sample(id: "refuse"))

        XCTAssertEqual(client.pendingOfferCommands.map(\.name), ["declinePreferredOffer"])
        XCTAssertEqual(client.pendingOfferCommands.first?.orderId, "refuse")
        XCTAssertEqual(rows(vm.state), ["keep"])
        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertNil(vm.attempt)
    }

    func testARefusedDeclineSaysSoOnTheSnackbarAndKeepsTheRow() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        let vm = makeVM()
        await vm.load()
        client.declineResult = .failure(ApiError(code: "order.not_found", httpStatus: 404))

        await vm.decline(.sample(id: "a"))

        XCTAssertEqual(snackbar.current?.severity, .error)
        XCTAssertEqual(rows(vm.state), ["a"])
        XCTAssertNil(vm.attempt)
        XCTAssertEqual(vm.actionState, .idle)
    }

    /// Confirming is `TakeOrder` — the shipped command with its one ordered `Cascade.Stop` chain, run by
    /// the contract sheet on the swipe. A UI that reached for anything else would have built a second,
    /// weaker take gate, so the confirm writes nothing here: it opens the sheet on the offer's own id,
    /// and only the sheet's verdict moves the list.
    func testConfirmingOpensTheContractSheetOnTheOfferAndWritesNothing() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        let vm = makeVM()
        await vm.load()

        vm.confirm(.sample(id: "a"))

        XCTAssertEqual(vm.contractRequest, .take(orderId: "a"))
        XCTAssertTrue(client.commands.isEmpty)
        XCTAssertTrue(client.pendingOfferCommands.isEmpty, "a confirm must not reach the decline endpoint")
        XCTAssertEqual(vm.actionState, .idle)
    }

    func testDismissingTheSheetLeavesTheOfferAsItWas() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        let vm = makeVM()
        await vm.load()
        vm.confirm(.sample(id: "a"))

        vm.dismissContract()

        XCTAssertNil(vm.contractRequest)
        XCTAssertEqual(rows(vm.state), ["a"])
        XCTAssertNil(vm.attempt)
    }

    func testATakenOutcomeClosesTheSheetAndOpensTheJob() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        let vm = makeVM()
        await vm.load()

        var opened: [String] = []
        vm.confirmed.sink { opened.append($0) }.store(in: &cancellables)

        await confirmed(vm, "a")

        XCTAssertNil(vm.contractRequest)
        XCTAssertTrue(client.pendingOfferCommands.isEmpty, "a confirm must not reach the decline endpoint")
        XCTAssertEqual(opened, ["a"])
        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertNil(vm.attempt)
    }

    /// A confirmed offer is an ordinary job from that instant on, so the board and the job the cleaner
    /// just acquired both have to refetch.
    func testAConfirmedOfferRestalesTheBoardAndTheOrder() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        let vm = makeVM()
        await vm.load()
        ordersStaleness.markPaneFresh(.available)
        ordersStaleness.markPaneFresh(.active)
        ordersStaleness.markOrderFresh("a")

        await confirmed(vm, "a")

        XCTAssertTrue(ordersStaleness.isPaneStale(.available))
        XCTAssertTrue(ordersStaleness.isPaneStale(.active))
        XCTAssertTrue(ordersStaleness.isOrderStale("a"))
    }

    /// The seam working as ruled: a reservation may not spend a cleaner's capacity, and the weekly cap
    /// IS capacity, so the cap is never consulted when the job is reserved — only when it is confirmed.
    /// Under a disclosed offer that is a visible broken promise, so the refusal is arranged here from
    /// the server's real key and must survive to the screen with its own reason intact.
    func testAConfirmTheWeeklyCapRefusesIsCarriedToTheScreenWithTheServersOwnReason() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        let vm = makeVM()
        await vm.load()

        await confirmRefused(vm, "a", weeklyCapKey)

        let expected = ApiErrorLocalizer().message(for: ApiError(code: weeklyCapKey, httpStatus: 400))
        XCTAssertNotEqual(expected, weeklyCapKey, "the cap's key must resolve to a sentence, not render raw")
        XCTAssertEqual(vm.actionState, .error(expected))
        XCTAssertEqual(vm.attempt?.orderId, "a")
        XCTAssertEqual(vm.attempt?.displayOrderNumber, "CL-a")
        XCTAssertEqual(vm.attempt?.action, .confirm)
    }

    /// The framed refusal owns this message. A snackbar as well would state the bare reason without the
    /// sentence that puts the failure on the platform, and the last one shown wins the cleaner's eye.
    func testARefusedConfirmDoesNotAlsoSnackbarTheBareReason() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        let vm = makeVM()
        await vm.load()

        await confirmRefused(vm, "a", weeklyCapKey)

        XCTAssertNil(snackbar.current)
    }

    func testARefusedConfirmReAsksTheServerRatherThanGuessingWhetherTheOfferSurvived() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        let vm = makeVM()
        await vm.load()
        let afterLoad = client.pendingOffersCallCount

        await confirmRefused(vm, "a", "order.no_available_spots")

        XCTAssertEqual(client.pendingOffersCallCount, afterLoad + 1)
        XCTAssertFalse(isError(vm.state))
    }

    func testARefusedConfirmIsFramedAsThePlatformsMistake() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        let vm = makeVM()
        await vm.load()

        await confirmRefused(vm, "a", weeklyCapKey)

        XCTAssertEqual(vm.refusal?.kind, .confirm)
        XCTAssertEqual(vm.refusal?.displayOrderNumber, "CL-a")
    }

    /// The list answers a refused release on the snackbar and keeps no attempt, so nothing here may
    /// reach for the framing the confirm owns.
    func testARefusedDeclineOnTheListRaisesNoFraming() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        let vm = makeVM()
        await vm.load()
        client.declineResult = .failure(ApiError(httpStatus: nil))

        await vm.decline(.sample(id: "a"))

        XCTAssertNil(vm.refusal)
        XCTAssertEqual(snackbar.current?.severity, .error)
    }

    func testDismissingTheRefusalClearsItWithoutTouchingTheList() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        let vm = makeVM()
        await vm.load()
        await confirmRefused(vm, "a", weeklyCapKey)

        vm.dismissRefusal()

        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertNil(vm.attempt)
        XCTAssertEqual(rows(vm.state), ["a"])
    }

    /// A confirm while a release is still in flight opens no sheet: the sheet would take a seat under a
    /// list that is mid-write. The take's own re-entry guard lives in the sheet.
    func testAConfirmWhileAReleaseIsInFlightOpensNoSheet() async {
        client.pendingOffersResult = .success([.sample(id: "a"), .sample(id: "b")])
        let vm = makeVM()
        await vm.load()
        client.suspendCommands = true

        let first = Task { await vm.decline(.sample(id: "a")) }
        while client.pendingOfferCommands.isEmpty {
            await Task.yield()
        }

        vm.confirm(.sample(id: "b"))

        XCTAssertNil(vm.contractRequest)
        XCTAssertEqual(vm.actionState, .submitting)
        XCTAssertEqual(vm.attempt?.action, .decline)

        client.resumeCommand()
        await first.value
    }

    func testAWarmCacheIsNotRefetchedOnEntry() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        _ = await store.refresh()
        let afterWarm = client.pendingOffersCallCount
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(client.pendingOffersCallCount, afterWarm)
        XCTAssertEqual(rows(vm.state), ["a"])
    }
}
