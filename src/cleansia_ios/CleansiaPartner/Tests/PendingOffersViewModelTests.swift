import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation
import XCTest
@testable import CleansiaPartner

/// "Confirming IS taking" — there is no confirm endpoint, and a UI that called anything else would be a
/// second acquisition path beside `TakeOrder`'s single ordered chain. The refusal cases matter as much
/// as the happy one: a reservation spends no capacity, so a capped cleaner can be reserved a job and
/// then refused the confirm, and that refusal has to read as the platform's problem.
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
        PendingOffersViewModel(
            store: store,
            client: client,
            staleness: ordersStaleness,
            snackbar: snackbar
        )
    }

    private func rows(_ state: UiState<[PendingOfferItem]>) -> [String] {
        (state.loadedValue ?? []).map { $0.id ?? "" }
    }

    private func isError(_ state: UiState<[PendingOfferItem]>) -> Bool {
        if case .error = state { return true }
        return false
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

    /// Confirming is `TakeOrder` — the shipped command with its one ordered `Cascade.Stop` chain. A UI
    /// that reached for anything else would have built a second, weaker take gate. This is the killer
    /// for that: the client seam records every command by name, so a confirm routed anywhere else
    /// changes this list.
    func testConfirmingTakesTheOrderThroughTakeOrderAndNothingElse() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        let vm = makeVM()
        await vm.load()

        var opened: [String] = []
        vm.confirmed.sink { opened.append($0) }.store(in: &cancellables)

        await vm.confirm(.sample(id: "a"))

        XCTAssertEqual(client.commands.map(\.name), ["take"])
        XCTAssertEqual(client.commands.map(\.orderId), ["a"])
        XCTAssertTrue(client.pendingOfferCommands.isEmpty, "a confirm must not reach the decline endpoint")
        XCTAssertEqual(opened, ["a"])
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

        await vm.confirm(.sample(id: "a"))

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
        client.commandResult = .failure(ApiError(code: weeklyCapKey, httpStatus: 400))

        await vm.confirm(.sample(id: "a"))

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
        client.commandResult = .failure(ApiError(code: weeklyCapKey, httpStatus: 400))

        await vm.confirm(.sample(id: "a"))

        XCTAssertNil(snackbar.current)
    }

    func testARefusedConfirmReAsksTheServerRatherThanGuessingWhetherTheOfferSurvived() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        let vm = makeVM()
        await vm.load()
        let afterLoad = client.pendingOffersCallCount
        client.commandResult = .failure(ApiError(code: "order.no_available_spots", httpStatus: 400))

        await vm.confirm(.sample(id: "a"))

        XCTAssertEqual(client.pendingOffersCallCount, afterLoad + 1)
        XCTAssertFalse(isError(vm.state))
    }

    func testDismissingTheRefusalClearsItWithoutTouchingTheList() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        let vm = makeVM()
        await vm.load()
        client.commandResult = .failure(ApiError(code: weeklyCapKey, httpStatus: 400))
        await vm.confirm(.sample(id: "a"))

        vm.dismissRefusal()

        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertNil(vm.attempt)
        XCTAssertEqual(rows(vm.state), ["a"])
    }

    /// The rival is awaited directly rather than raced on a second task, so its body has provably run
    /// past the guard before anything is asserted — a "not called" assertion made before the rival
    /// dispatched passes with no guard at all.
    func testASecondActionWhileOneIsInFlightIsRefused() async {
        client.pendingOffersResult = .success([.sample(id: "a"), .sample(id: "b")])
        let vm = makeVM()
        await vm.load()
        client.suspendCommands = true

        let first = Task { await vm.confirm(.sample(id: "a")) }
        while client.commands.isEmpty {
            await Task.yield()
        }

        await vm.decline(.sample(id: "b"))

        XCTAssertTrue(client.pendingOfferCommands.isEmpty)
        XCTAssertEqual(vm.actionState, .submitting)
        XCTAssertEqual(vm.attempt?.action, .confirm)

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
