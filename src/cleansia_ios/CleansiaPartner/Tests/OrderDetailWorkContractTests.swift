import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The detail's half of the contract for work: the take opens the sheet instead of writing, the
/// sheet's verdict is reconciled exactly as the one-tap take was, a seat an administrator placed sees
/// the banner, and a Start or Complete the server refuses for want of an acceptance opens the same
/// sheet in accept mode rather than snackbarring.
@MainActor
final class OrderDetailWorkContractTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var staleness: OrdersStaleness!
    private var snackbar: SnackbarController!

    private let acceptedOn = Date(timeIntervalSince1970: 1_786_000_000)

    override func setUp() {
        super.setUp()
        client = FakePartnerOrderClient()
        staleness = OrdersStaleness()
        snackbar = SnackbarController()
    }

    private func makeVM() -> OrderDetailViewModel {
        OrderDetailViewModel(
            orderId: "order-1",
            client: client,
            staleness: staleness,
            snackbar: snackbar,
            pendingOffers: PendingOffersStore(client: client, ordersStaleness: staleness)
        )
    }

    private func loaded(
        status: Int = 2,
        isMine: Bool = true,
        seated: Bool = true,
        acceptanceId: String? = nil
    ) -> OrderItem {
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: status)
        item.isAssignedToCurrentUser = isMine
        item.hasAfterPhotos = true
        item.assignedEmployees = seated ? [AssignedEmployeeDto(id: "seat-1", employeeId: "emp-self")] : []
        item.workContractAcceptances = acceptanceId.map {
            [WorkContractAcceptanceDto(
                id: $0,
                orderEmployeeId: "seat-1",
                acceptedOn: acceptedOn,
                documentVersion: "v1"
            )]
        } ?? []
        return item
    }

    // MARK: the take opens the sheet

    func testTakeOpensTheSheetForThisOrderAndWritesNothing() async {
        client.byIdResult = .success(loaded(status: 2, isMine: false, seated: false))
        let vm = makeVM()
        await vm.load()

        vm.take()

        XCTAssertEqual(vm.contractRequest, .take(orderId: "order-1"))
        XCTAssertTrue(client.commands.isEmpty)
        XCTAssertEqual(vm.actionState, .idle)
    }

    func testDispatchingTheTakeActionOpensTheSheetToo() async {
        client.byIdResult = .success(loaded(status: 2, isMine: false, seated: false))
        let vm = makeVM()
        await vm.load()

        await vm.dispatch(.take)

        XCTAssertEqual(vm.contractRequest, .take(orderId: "order-1"))
        XCTAssertTrue(client.commands.isEmpty)
    }

    func testDismissingTheSheetClearsTheRequest() {
        let vm = makeVM()
        vm.take()

        vm.dismissContract()

        XCTAssertNil(vm.contractRequest)
    }

    /// The sheet took the seat; the detail reconciles as it did after its own take — silent, the panes
    /// invalidated, the order refetched.
    func testATakenOutcomeClosesTheSheetAndReconcilesLikeATake() async {
        client.byIdResult = .success(loaded(status: 2, isMine: false, seated: false))
        let vm = makeVM()
        await vm.load()
        vm.take()
        for pane in OrdersPane.allCases {
            staleness.markPaneFresh(pane)
        }
        let fetchesBefore = client.getByIdCallCount

        await vm.onWorkContractOutcome(.taken(orderId: "order-1"))

        XCTAssertNil(vm.contractRequest)
        XCTAssertEqual(client.getByIdCallCount, fetchesBefore + 1)
        XCTAssertTrue(staleness.isPaneStale(.available))
        XCTAssertTrue(staleness.isPaneStale(.active))
        XCTAssertFalse(staleness.isPaneStale(.history))
        XCTAssertNil(snackbar.current)
        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertNil(vm.inFlightAction)
    }

    func testARefusedTakeOutcomeIsSnackbarredAndTheOrderRefetched() async {
        client.byIdResult = .success(loaded(status: 2, isMine: false, seated: false))
        let vm = makeVM()
        await vm.load()
        vm.take()
        let fetchesBefore = client.getByIdCallCount
        let refusal = ApiError(code: "order.no_available_spots", httpStatus: 400)

        await vm.onWorkContractOutcome(.refused(.take(orderId: "order-1"), refusal))

        XCTAssertNil(vm.contractRequest)
        XCTAssertEqual(snackbar.current?.severity, .error)
        XCTAssertEqual(client.getByIdCallCount, fetchesBefore + 1)
        guard case .error = vm.actionState else { return XCTFail("expected the action error") }
    }

    /// On a disclosed reservation the screen frames the refusal in its own words, exactly as it framed
    /// the one-tap confirm — the sheet changes where the take runs, not how its refusal reads.
    func testARefusedTakeOnADisclosedOfferIsFramedNotSnackbarred() async {
        client.byIdResult = .success(loaded(status: 2, isMine: false, seated: false))
        client.pendingOffersResult = .success([.sample(id: "order-1")])
        let vm = makeVM()
        await vm.load()
        vm.take()
        let refusal = ApiError(code: "order.weekly_limit_reached", httpStatus: 400)

        await vm.onWorkContractOutcome(.refused(.take(orderId: "order-1"), refusal))

        XCTAssertEqual(vm.refusal?.kind, .confirm)
        XCTAssertNil(snackbar.current)
    }

    func testAnAcceptedOutcomeInvalidatesTheActivePaneAndRefetches() async {
        client.byIdResult = .success(loaded(status: 2))
        let vm = makeVM()
        await vm.load()
        vm.openContract(.accept(orderId: "order-1"))
        for pane in OrdersPane.allCases {
            staleness.markPaneFresh(pane)
        }
        let fetchesBefore = client.getByIdCallCount

        await vm.onWorkContractOutcome(.accepted(orderId: "order-1"))

        XCTAssertNil(vm.contractRequest)
        XCTAssertEqual(client.getByIdCallCount, fetchesBefore + 1)
        XCTAssertTrue(staleness.isPaneStale(.active))
        XCTAssertFalse(staleness.isPaneStale(.available))
        XCTAssertFalse(staleness.isPaneStale(.history))
        XCTAssertNil(snackbar.current)
    }

    // MARK: the standing

    func testASeatWithNoRowShowsTheBannerOnceMyIdIsResolved() async {
        client.byIdResult = .success(loaded(status: 2, seated: true))
        let vm = makeVM()
        XCTAssertEqual(vm.contractStanding, .none)

        await vm.load()

        XCTAssertEqual(client.employeeIdCallCount, 1)
        XCTAssertEqual(vm.contractStanding, .pending)
    }

    func testASeatWithARowShowsTheLine() async {
        client.byIdResult = .success(loaded(status: 2, acceptanceId: "acc-1"))
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(
            vm.contractStanding,
            .accepted(acceptanceId: "acc-1", acceptedOn: acceptedOn, documentVersion: "v1")
        )
    }

    /// The line replaces the banner through the refetch the acceptance triggers, never through a local
    /// flip: the server's row is the fact.
    func testTheLineReplacesTheBannerAfterTheAcceptanceRefetch() async {
        client.byIdResult = .success(loaded(status: 2))
        let vm = makeVM()
        await vm.load()
        XCTAssertEqual(vm.contractStanding, .pending)
        client.byIdResult = .success(loaded(status: 2, acceptanceId: "acc-1"))

        await vm.onWorkContractOutcome(.accepted(orderId: "order-1"))

        XCTAssertEqual(
            vm.contractStanding,
            .accepted(acceptanceId: "acc-1", acceptedOn: acceptedOn, documentVersion: "v1")
        )
    }

    func testABrowsingCleanerHasNoStanding() async {
        client.byIdResult = .success(loaded(status: 2, isMine: false, seated: false))
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(vm.contractStanding, .none)
    }

    func testReadOpensTheSheetOnTheAcceptance() {
        let vm = makeVM()

        vm.openContract(.read(acceptanceId: "acc-1"))

        XCTAssertEqual(vm.contractRequest, .read(acceptanceId: "acc-1"))
    }

    // MARK: the gates open the sheet in accept mode

    func testAStartRefusedForWantOfAnAcceptanceOpensTheSheetInAcceptMode() async {
        client.byIdResult = .success(loaded(status: 3))
        let vm = makeVM()
        await vm.load()
        client.commandResult = .failure(ApiError(code: WorkContractErrorKey.acceptanceRequired, httpStatus: 400))
        let fetchesBefore = client.getByIdCallCount

        await vm.start()

        XCTAssertEqual(client.commands.map(\.name), ["start"])
        XCTAssertEqual(vm.contractRequest, .accept(orderId: "order-1"))
        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertNil(vm.inFlightAction)
        XCTAssertNil(snackbar.current)
        XCTAssertEqual(client.getByIdCallCount, fetchesBefore)
    }

    func testACompleteRefusedForWantOfAnAcceptanceOpensTheSheetInAcceptMode() async {
        client.byIdResult = .success(loaded(status: 4))
        let vm = makeVM()
        await vm.load()
        client.commandResult = .failure(ApiError(code: WorkContractErrorKey.acceptanceRequired, httpStatus: 400))

        await vm.complete()

        XCTAssertEqual(client.commands.map(\.name), ["complete"])
        XCTAssertEqual(vm.contractRequest, .accept(orderId: "order-1"))
        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertNil(snackbar.current)
    }

    /// The same key on any other action is an ordinary refusal — nothing else on this screen has a
    /// contract to open.
    func testTheSameKeyOnAnotherActionStaysAnOrdinaryRefusal() async {
        client.byIdResult = .success(loaded(status: 2))
        let vm = makeVM()
        await vm.load()
        client.commandResult = .failure(ApiError(code: WorkContractErrorKey.acceptanceRequired, httpStatus: 400))

        await vm.notifyOnTheWay()

        XCTAssertNil(vm.contractRequest)
        XCTAssertEqual(snackbar.current?.severity, .error)
    }

    func testAnyOtherStartRefusalStillSnackbarsAndRefetches() async {
        client.byIdResult = .success(loaded(status: 3))
        let vm = makeVM()
        await vm.load()
        client.commandResult = .failure(ApiError(code: "order.employee_not_assigned", httpStatus: 400))
        let fetchesBefore = client.getByIdCallCount

        await vm.start()

        XCTAssertNil(vm.contractRequest)
        XCTAssertEqual(snackbar.current?.severity, .error)
        XCTAssertEqual(client.getByIdCallCount, fetchesBefore + 1)
    }

    /// Start and Complete stay offered on a pending seat — the server's gate is the authority and the
    /// refusal is what opens the sheet.
    func testStartStaysOfferedOnAPendingSeat() async {
        client.byIdResult = .success(loaded(status: 3))
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(vm.contractStanding, .pending)
        XCTAssertEqual(vm.primaryAction, .start)
    }
}
