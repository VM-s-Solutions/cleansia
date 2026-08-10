import CleansiaCore
import CleansiaPartnerApi
import Foundation
import XCTest
@testable import CleansiaPartner

/// The `order.preferred_offer` push still deep-links to the order detail, because the push fires on a
/// wider predicate than the reservation does — a short-lead recipient and a card order whose money has
/// not landed both get the push with no pending offer behind them, and a link to the offers list would
/// land them on an empty screen. So the detail is where the disclosure and the decline have to live.
@MainActor
final class OrderDetailPreferredOfferTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var ordersStaleness: OrdersStaleness!
    private var snackbar: SnackbarController!
    private var store: PendingOffersStore!

    private let orderId = "order-1"

    override func setUp() async throws {
        client = FakePartnerOrderClient()
        ordersStaleness = OrdersStaleness()
        snackbar = SnackbarController()
        store = PendingOffersStore(client: client, ordersStaleness: ordersStaleness)
        client.byIdResult = .success(loadedOrder())
    }

    private func loadedOrder() -> OrderItem {
        var item = OrderItem()
        item.id = orderId
        item.orderStatus = Code(value: 2)
        item.displayOrderNumber = "CL-order-1"
        return item
    }

    private func makeVM() -> OrderDetailViewModel {
        OrderDetailViewModel(
            orderId: orderId,
            client: client,
            staleness: ordersStaleness,
            snackbar: snackbar,
            pendingOffers: store
        )
    }

    func testTheDetailDisclosesAReservationHeldForThisOrderAlone() async {
        client.pendingOffersResult = .success([.sample(id: "other-order"), .sample(id: orderId)])
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(vm.preferredOffer?.id, orderId)
    }

    func testAnOrderNobodyReservedDisclosesNothing() async {
        client.pendingOffersResult = .success([.sample(id: "other-order")])
        let vm = makeVM()

        await vm.load()

        XCTAssertNil(vm.preferredOffer)
    }

    /// The 2–8 hour band pushes a named cleaner without withholding a seat, and a card order is pushed
    /// before its money lands. Both reach this screen with no reservation behind them, and the screen
    /// has to be an ordinary job in that case rather than claim one.
    func testAStaleOffersCacheIsFilledSoTheDisclosureIsNotSilentlyAbsent() async {
        client.pendingOffersResult = .success([.sample(id: orderId)])
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(client.pendingOffersCallCount, 1)
        XCTAssertEqual(vm.preferredOffer?.id, orderId)
    }

    func testAWarmOffersCacheIsNotRefetchedByTheDetail() async {
        client.pendingOffersResult = .success([.sample(id: orderId)])
        _ = await store.refresh()
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(client.pendingOffersCallCount, 1)
        XCTAssertEqual(vm.preferredOffer?.id, orderId)
    }

    func testDecliningFromTheDetailCallsTheDeclineEndpointAndTheDisclosureGoes() async {
        client.pendingOffersResult = .success([.sample(id: orderId)])
        let vm = makeVM()
        await vm.load()
        client.onDeclinePreferredOffer = { [weak self] _ in
            self?.client.pendingOffersResult = .success([])
        }

        await vm.declinePreferredOffer()

        XCTAssertEqual(client.pendingOfferCommands.map(\.name), ["declinePreferredOffer"])
        XCTAssertEqual(client.pendingOfferCommands.first?.orderId, orderId)
        XCTAssertNil(vm.preferredOffer)
        XCTAssertEqual(snackbar.current?.severity, .success)
    }

    /// Nothing gates the reservation on the weekly cap, so the take gate can refuse a job the cleaner
    /// was told was theirs. On a disclosed offer that refusal is framed by the screen as the platform's
    /// mistake, so it must not also arrive as a bare snackbar line.
    func testARefusedConfirmOnADisclosedOfferIsStateNotABareSnackbar() async {
        client.pendingOffersResult = .success([.sample(id: orderId)])
        let vm = makeVM()
        await vm.load()
        client.commandResult = .failure(ApiError(code: "order.weekly_limit_reached", httpStatus: 400))

        await vm.take()

        let expected = ApiErrorLocalizer()
            .message(for: ApiError(code: "order.weekly_limit_reached", httpStatus: 400))
        XCTAssertEqual(vm.actionState, .error(expected))
        XCTAssertNil(snackbar.current)
    }

    func testARefusedTakeOnAnOrdinaryJobStillReachesTheSnackbar() async {
        client.pendingOffersResult = .success([])
        let vm = makeVM()
        await vm.load()
        client.commandResult = .failure(ApiError(code: "order.no_available_spots", httpStatus: 400))

        await vm.take()

        XCTAssertEqual(snackbar.current?.severity, .error)
    }

    /// A refused CONFIRM is the platform's fault — nothing gates the reservation on the weekly cap, so
    /// the take gate can refuse a job the cleaner was told was theirs, and the framing takes the blame.
    func testARefusedConfirmIsFramedAsThePlatformsMistake() async {
        client.pendingOffersResult = .success([.sample(id: orderId)])
        let vm = makeVM()
        await vm.load()
        client.commandResult = .failure(ApiError(code: "order.weekly_limit_reached", httpStatus: 400))

        await vm.take()

        XCTAssertEqual(vm.refusal?.kind, .confirm)
        XCTAssertEqual(vm.refusal?.displayOrderNumber, "CL-\(orderId)")
    }

    /// A refused RELEASE is a different failure and may not wear the blame written for the other one:
    /// the write simply did not land, nothing was released, and there is no mistake to own in either
    /// direction.
    func testARefusedReleaseIsNotFramedAsTheConfirmsMistake() async {
        client.pendingOffersResult = .success([.sample(id: orderId)])
        let vm = makeVM()
        await vm.load()
        client.declineResult = .failure(ApiError(httpStatus: nil))

        await vm.declinePreferredOffer()

        XCTAssertEqual(vm.refusal?.kind, .release)
        XCTAssertNotNil(vm.preferredOffer, "a refused release leaves the reservation exactly as it was")
    }

    /// The screen frames this one too, so the bare reason must not arrive a second time on top of it.
    func testARefusedReleaseOnADisclosedOfferIsStateNotABareSnackbar() async {
        client.pendingOffersResult = .success([.sample(id: orderId)])
        let vm = makeVM()
        await vm.load()
        client.declineResult = .failure(ApiError(httpStatus: nil))

        await vm.declinePreferredOffer()

        XCTAssertNil(snackbar.current)
    }

    /// Only the two reservation actions earn the framing. Any other refusal on this screen already
    /// went to the snackbar and must keep going there, even while a reservation is disclosed.
    func testANonReservationRefusalNeverWearsTheOfferFraming() async {
        client.pendingOffersResult = .success([.sample(id: orderId)])
        let vm = makeVM()
        await vm.load()
        client.commandResult = .failure(ApiError(code: "order.not_in_progress", httpStatus: 400))

        await vm.notifyOnTheWay()

        XCTAssertNil(vm.refusal)
        XCTAssertEqual(snackbar.current?.severity, .error)
    }

    func testAnOrdinaryJobNeverProducesAFramedRefusal() async {
        client.pendingOffersResult = .success([])
        let vm = makeVM()
        await vm.load()
        client.commandResult = .failure(ApiError(code: "order.no_available_spots", httpStatus: 400))

        await vm.take()

        XCTAssertNil(vm.refusal)
    }

    func testDismissingTheFramedRefusalClearsIt() async {
        client.pendingOffersResult = .success([.sample(id: orderId)])
        let vm = makeVM()
        await vm.load()
        client.declineResult = .failure(ApiError(httpStatus: nil))
        await vm.declinePreferredOffer()

        vm.dismissActionError()

        XCTAssertNil(vm.refusal)
    }

    func testDismissingTheFramedRefusalClearsTheActionError() async {
        client.pendingOffersResult = .success([.sample(id: orderId)])
        let vm = makeVM()
        await vm.load()
        client.commandResult = .failure(ApiError(code: "order.weekly_limit_reached", httpStatus: 400))
        await vm.take()

        vm.dismissActionError()

        XCTAssertEqual(vm.actionState, .idle)
    }
}
