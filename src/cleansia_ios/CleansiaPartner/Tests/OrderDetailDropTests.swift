import CleansiaCore
import CleansiaPartnerApi
import Combine
import SwiftUI
import XCTest
@testable import CleansiaPartner

/// "Cancel job": the cleaner leaves the crew of a job they took. The seat goes back on the board and the
/// customer's booking stands; the server says how many cleaners are left on it.
@MainActor
final class OrderDetailDropTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var staleness: OrdersStaleness!
    private var snackbar: SnackbarController!
    private var cancellables: [AnyCancellable] = []

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

    private func job(status: Int = 2, isMine: Bool = true, lockoutReportedAt: Date? = nil) -> OrderItem {
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: status)
        item.isAssignedToCurrentUser = isMine
        item.lockoutReportedAt = lockoutReportedAt
        return item
    }

    private func loaded(_ item: OrderItem) async -> OrderDetailViewModel {
        client.byIdResult = .success(item)
        let vm = makeVM()
        await vm.load()
        return vm
    }

    private func leaves(_ vm: OrderDetailViewModel) -> () -> Int {
        var count = 0
        let token = vm.left.sink { count += 1 }
        cancellables.append(token)
        return { count }
    }

    // MARK: - Who is offered it

    func testOfferedOnlyToTheCrewOfAJobNotYetUnderWay() async {
        let cases: [DropCase] = [
            DropCase(item: job(status: 0), offered: true, why: "mine, New"),
            DropCase(item: job(status: 2), offered: true, why: "mine, Confirmed"),
            DropCase(item: job(status: 3), offered: false, why: "on the way: the cleaner has set off"),
            DropCase(
                item: job(status: 4),
                offered: false,
                why: "in progress: the last cleaner leaving would strand it"
            ),
            DropCase(item: job(status: 5), offered: false, why: "completed"),
            DropCase(item: job(status: 6), offered: false, why: "cancelled"),
            DropCase(item: job(status: 1), offered: false, why: "pending has no writer"),
            DropCase(item: job(isMine: false), offered: false, why: "not on the crew"),
            DropCase(
                item: job(lockoutReportedAt: Date(timeIntervalSince1970: 1_786_000_000)),
                offered: false,
                why: "a lockout report pays the seat a drop would delete"
            )
        ]
        for testCase in cases {
            let vm = await loaded(testCase.item)
            XCTAssertEqual(vm.canDropOrder, testCase.offered, testCase.why)
        }
    }

    func testNotOfferedBeforeTheOrderLoads() {
        XCTAssertFalse(makeVM().canDropOrder)
    }

    /// A Confirmed job's crew member sees it under "On my way"; a New one has no other action, so the link
    /// gets a footer of its own.
    func testTheLinkSitsUnderNotifyAndAloneOnAnAssignedNewJob() async {
        let confirmed = await loaded(job(status: 2))
        XCTAssertEqual(confirmed.primaryAction, .notifyOnTheWay)
        let new = await loaded(job(status: 0))
        XCTAssertEqual(new.primaryAction, OrderPrimaryAction.none)

        XCTAssertEqual(footerHeight(.none, canDrop: false), 0)
        XCTAssertGreaterThan(footerHeight(.none, canDrop: true), 0)
        XCTAssertGreaterThan(
            footerHeight(.notifyOnTheWay, canDrop: true),
            footerHeight(.notifyOnTheWay, canDrop: false)
        )
    }

    /// The spinner that replaces the link while the drop is sent takes the link's height.
    func testTheFooterKeepsItsHeightWhileTheDropIsSent() {
        XCTAssertEqual(
            footerHeight(.notifyOnTheWay, canDrop: true, inFlight: .drop),
            footerHeight(.notifyOnTheWay, canDrop: true),
            accuracy: 0.5
        )
    }

    private func footerHeight(_ action: OrderPrimaryAction, canDrop: Bool, inFlight: OrderAction? = nil) -> CGFloat {
        let footer = StickyActionFooter(
            action: action,
            inFlightAction: inFlight,
            onConfirm: { _ in },
            canDropOrder: canDrop
        )
        return UIHostingController(rootView: footer)
            .sizeThatFits(in: CGSize(width: 390, height: CGFloat.greatestFiniteMagnitude))
            .height
    }

    // MARK: - Leaving

    func testTheLastCleanerLeavingIsToldTheJobHasNoCleanerAndTheScreenLeaves() async {
        let vm = await loaded(job())
        client.dropResult = .success(0)
        for pane in OrdersPane.allCases {
            staleness.markPaneFresh(pane)
        }
        staleness.markOrderFresh("order-1")
        let fetchesBefore = client.getByIdCallCount
        let leftCount = leaves(vm)

        await vm.dropOrder()

        XCTAssertEqual(client.dropCalls, ["order-1"])
        XCTAssertEqual(snackbar.current?.severity, .success)
        XCTAssertEqual(snackbar.current?.text, L10n.Orders.droppedNoCrewToast)
        XCTAssertEqual(leftCount(), 1)
        XCTAssertTrue(staleness.isPaneStale(.available))
        XCTAssertTrue(staleness.isPaneStale(.active))
        XCTAssertFalse(staleness.isPaneStale(.history))
        XCTAssertTrue(staleness.isOrderStale("order-1"))
        XCTAssertEqual(client.getByIdCallCount, fetchesBefore, "a job the cleaner left is not theirs to re-read")
        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertNil(vm.inFlightAction)
    }

    func testLeavingACrewThatStillHasSomeoneSaysThePlaceIsBackOnTheBoard() async {
        let vm = await loaded(job())
        client.dropResult = .success(1)
        let leftCount = leaves(vm)

        await vm.dropOrder()

        XCTAssertEqual(snackbar.current?.text, L10n.Orders.droppedToast)
        XCTAssertNotEqual(L10n.Orders.droppedToast, L10n.Orders.droppedNoCrewToast)
        XCTAssertEqual(leftCount(), 1)
    }

    /// A drop that lost a race to a status change is refused; the screen stays and shows the job as it is now.
    func testARefusedDropKeepsTheScreenSnackbarsTheReasonAndReReadsTheJob() async {
        let vm = await loaded(job())
        client.dropResult = .failure(ApiError(code: "order.not_confirmed", httpStatus: 400))
        let fetchesBefore = client.getByIdCallCount
        let leftCount = leaves(vm)

        await vm.dropOrder()

        XCTAssertEqual(leftCount(), 0)
        XCTAssertEqual(snackbar.current?.severity, .error)
        XCTAssertEqual(client.getByIdCallCount, fetchesBefore + 1)
        guard case .error = vm.actionState else { return XCTFail("expected action error") }
        XCTAssertNil(vm.inFlightAction)
        XCTAssertNil(vm.refusal, "a refused drop is never framed as a refused reservation")
    }

    func testTheDropHoldsItsOwnInFlightDiscriminatorAndASecondTapIsDropped() async {
        let vm = await loaded(job())
        client.suspendCommands = true

        let dropping = Task { await vm.dropOrder() }
        while client.dropCalls.isEmpty {
            await Task.yield()
        }
        XCTAssertEqual(vm.inFlightAction, .drop)
        XCTAssertTrue(vm.actionState.isSubmitting)

        await vm.dropOrder()
        await vm.notifyOnTheWay()
        XCTAssertEqual(client.dropCalls.count, 1)
        XCTAssertTrue(client.commands.isEmpty, "nothing else is sent while the drop is in flight")

        client.resumeCommand()
        await dropping.value
        XCTAssertNil(vm.inFlightAction)
    }

    func testADropIsNotSentWhileAnotherActionIsInFlight() async {
        let vm = await loaded(job())
        client.suspendCommands = true

        let notifying = Task { await vm.notifyOnTheWay() }
        while client.commands.isEmpty {
            await Task.yield()
        }
        await vm.dropOrder()
        XCTAssertTrue(client.dropCalls.isEmpty)

        client.resumeCommand()
        await notifying.value
    }

    func testTheDropSendsOnlyTheOrderItWasOpenedOn() async {
        let vm = await loaded(job())

        await vm.dropOrder()

        XCTAssertEqual(client.dropCalls, ["order-1"])
        XCTAssertTrue(client.commands.isEmpty)
    }
}

/// One row of the offer table: the job, whether leaving it is offered, and why.
private struct DropCase {
    let item: OrderItem
    let offered: Bool
    let why: String
}
