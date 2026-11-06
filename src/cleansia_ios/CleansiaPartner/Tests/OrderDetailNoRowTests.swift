import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import XCTest
@testable import CleansiaPartner

/// Every entry to the detail carries only the order id; the board-rows store is the one place a row comes from.
/// Found, the detail draws from it at once and has the band; not found (a push, the Dashboard), nothing is known
/// until `GetById` answers, and a job not taken waits for her own rows before it offers Take.
@MainActor
final class OrderDetailNoRowTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var staleness: OrdersStaleness!
    private var store: OrdersBoardStore!
    private let start = Date(timeIntervalSince1970: 1_786_000_000)

    override func setUp() {
        super.setUp()
        client = FakePartnerOrderClient()
        staleness = OrdersStaleness()
        store = OrdersBoardStore()
    }

    private func makeVM(orderId: String = "V4") -> OrderDetailViewModel {
        OrderDetailViewModel(
            orderId: orderId,
            client: client,
            staleness: staleness,
            snackbar: SnackbarController(),
            pendingOffers: PendingOffersStore(client: client, ordersStaleness: staleness),
            boardStore: store
        )
    }

    private func openJob(from offset: TimeInterval = 0) -> OrderItem {
        var item = OrderItem.wireComplete()
        item.id = "V4"
        item.orderStatus = Code(value: 0)
        item.cleaningDateTime = start.addingTimeInterval(offset)
        item.estimatedTime = 120
        return item
    }

    private func own(_ id: String, from offset: TimeInterval, minutes: Int = 180) -> OrderListItem {
        var row = OrderListItem.sample(id: id, status: ._2, cleaningDateTime: start.addingTimeInterval(offset))
        row.estimatedTime = minutes
        return row
    }

    func testARowTheBoardHoldsIsFoundWithItsBand() {
        var row = OrderListItem.sample(id: "V4", cleaningDateTime: start)
        row.homeDistanceBand = ._2
        store.publish(available: [row])

        let vm = makeVM()

        XCTAssertEqual(vm.boardRow?.id, "V4")
        XCTAssertEqual(vm.band, ._2)
    }

    func testWithNoRowThereIsNoBand() {
        store.publish(available: [OrderListItem.sample(id: "V3")])

        let vm = makeVM()

        XCTAssertNil(vm.boardRow)
        XCTAssertNil(vm.band)
    }

    func testTakeWaitsForHerRowsThenRefusesTheOverlap() async {
        client.byIdResult = .success(openJob())
        client.pageResponder = { _ in .success(OrderPage(rows: [self.own("A2", from: 3600)], total: 1)) }
        client.suspendGetPaged = true
        let vm = makeVM()

        let loading = Task { await vm.load() }
        while client.getPagedCallCount == 0 {
            await Task.yield()
        }
        XCTAssertEqual(vm.primaryAction, .takeWaiting)

        client.resumeGetPaged()
        await loading.value

        XCTAssertEqual(vm.clashingJob?.id, "A2")
        XCTAssertEqual(
            vm.primaryAction,
            .takeOverlaps(start: start.addingTimeInterval(3600), end: start.addingTimeInterval(3600 + 10800))
        )
        XCTAssertEqual(client.queries.last?.statuses, [._2, ._3, ._4])
        XCTAssertEqual(client.queries.last?.employeeId, "emp-self")
    }

    /// The job answers before her own id does, so her rows are not even asked for yet: Take already waits, or a tap
    /// in that gap would reach the server before the clash check.
    func testTakeWaitsFromTheMomentTheJobAnswersWhileHerIdIsStillBeingRead() async {
        client.byIdResult = .success(openJob())
        client.pageResponder = { _ in .success(OrderPage(rows: [self.own("A2", from: 3600)], total: 1)) }
        client.suspendEmployeeId = true
        let vm = makeVM()

        let loading = Task { await vm.load() }
        while vm.state.loadedValue == nil {
            await Task.yield()
        }
        XCTAssertEqual(vm.primaryAction, .takeWaiting)

        client.resumeEmployeeId()
        await loading.value

        XCTAssertEqual(vm.clashingJob?.id, "A2")
    }

    func testAFailedReadOffersTakeAndLeavesTheClashToTheServer() async {
        client.byIdResult = .success(openJob())
        client.pageResponder = { _ in .failure(ApiError(code: "network.unreachable")) }
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(vm.primaryAction, .take)
    }

    func testTheBoardsOwnRowsAreUsedWithoutAReadAndTouchingWindowsDoNotClash() async {
        client.byIdResult = .success(openJob())
        store.publish(own: [own("A1", from: 7200, minutes: 60)])
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(client.getPagedCallCount, 0)
        XCTAssertNil(vm.clashingJob, "a job that starts when this one ends does not overlap it")
        XCTAssertEqual(vm.primaryAction, .take)
    }

    func testHerOwnJobReadsNoRows() async {
        var item = openJob()
        item.orderStatus = Code(value: 2)
        item.isAssignedToCurrentUser = true
        client.byIdResult = .success(item)
        let vm = makeVM()

        await vm.load()

        XCTAssertNil(vm.ownRows)
        XCTAssertEqual(client.getPagedCallCount, 0)
    }

    /// Refused while the board still holds her row (an administrator gave the job to someone else): the job is gone,
    /// and nothing is drawn from that row, so its customer and address do not stay on screen as hers.
    func testAJobTheServerRefusesIsNotDrawnFromHerStoredRow() async {
        store.publish(own: [own("A1", from: 0)])
        client.byIdResult = .failure(ApiError(code: "order.not_found", httpStatus: 404))
        let vm = makeVM(orderId: "A1")
        XCTAssertEqual(vm.rowDetail?.id, "A1", "while the job loads, the board's row draws it")

        await vm.load()

        XCTAssertTrue(vm.isGone)
        XCTAssertNil(vm.rowDetail)
    }

    /// A push opened after someone took the job: retrying cannot change the answer.
    func testAJobNoLongerThereIsGone() async {
        client.byIdResult = .failure(ApiError(code: "order.not_found", httpStatus: 404))
        let vm = makeVM()

        await vm.load()

        XCTAssertTrue(vm.isGone)
        client.byIdResult = .failure(ApiError(code: "network.unreachable"))
        let failed = makeVM()
        await failed.load()
        XCTAssertFalse(failed.isGone)
    }

    /// A job gone says only that: no connection hint and no Retry, since neither could change the answer. A load that
    /// failed keeps both.
    func testTheGonePageSaysOnlyThatTheJobIsNoLongerOpen() async throws {
        let hint = HostedSpeech.plain(L10n.Orders.errorMessage)
        let gone = try await HostedSpeech.labels(of: NoRowFailure(
            isGone: true,
            now: start,
            ownRows: nil,
            openRows: nil,
            onRetry: {}
        ))
        XCTAssertTrue(gone.contains(HostedSpeech.plain(L10n.Orders.gone)), "\(gone)")
        XCTAssertFalse(gone.contains(hint), "\(gone)")
        XCTAssertFalse(gone.contains(L10n.retry), "\(gone)")

        let failed = try await HostedSpeech.labels(of: NoRowFailure(
            isGone: false,
            now: start,
            ownRows: nil,
            openRows: nil,
            onRetry: {}
        ))
        XCTAssertTrue(failed.contains(hint), "\(failed)")
        XCTAssertTrue(failed.contains(L10n.retry), "\(failed)")
    }

    /// With no board row for this job the store's rows of that day are older than the push: her next jobs stand in
    /// their place. With the row, the day's sections.
    func testWithNoBoardRowAJobNotTakenShowsHerNextJobsNotTheDay() async throws {
        let order = try OrderDetail(openJob())
        let mine = [own("A1", from: 86400)]
        var also = OrderListItem.sample(id: "V5", status: ._0, cleaningDateTime: start.addingTimeInterval(7200))
        also.estimatedTime = 60
        let day = [L10n.Orders.openToday, L10n.Orders.alsoOpenThatDay].map(HostedSpeech.plain)
        func sections(boardRow: Bool) async throws -> [String] {
            try await HostedSpeech.labels(of: VStack {
                NotTakenSections(
                    order: order,
                    now: self.start.addingTimeInterval(-3600),
                    ownRows: mine,
                    openRows: [also],
                    clash: nil,
                    hasBoardRow: boardRow
                )
            })
        }

        let pushed = try await sections(boardRow: false)
        XCTAssertTrue(pushed.contains { $0.contains(HostedSpeech.plain(L10n.Orders.yourNextJobs)) }, "\(pushed)")
        XCTAssertFalse(pushed.contains { label in day.contains { label.contains($0) } }, "\(pushed)")

        let fromBoard = try await sections(boardRow: true)
        XCTAssertTrue(fromBoard.contains { label in day.contains { label.contains($0) } }, "\(fromBoard)")
    }

    /// A job not hers draws one finding: her clashing job first, else the hold, else the late join's flag.
    func testAJobNotTakenDrawsOneFindingTheClashFirst() async throws {
        var item = openJob()
        item.orderStatus = Code(value: 4)
        item.assignedEmployees = [AssignedEmployeeDto(id: "seat-0", employeeId: "emp-marta", fullName: "Marta")]
        let order = try OrderDetail(item)
        let english = Locale(identifier: "en")
        let link = HostedSpeech.plain(L10n.Orders.yourJobThatDay)
        let hold = HostedSpeech.plain(L10n.Offers.cardTitle)
        let join = HostedSpeech.plain(L10n.Orders.flagTakeUntil(
            OrdersFormat.timeOnly(start.addingTimeInterval(7200), locale: english)
        ))
        func findings(clash: OrderListItem?, offer: PendingOfferItem?) async throws -> [String] {
            try await HostedSpeech.labels(of: OrderDetailContent(
                order: order,
                now: start.addingTimeInterval(600),
                clash: clash,
                preferredOffer: offer,
                checklistVM: .preview,
                notesVM: .preview,
                photosVM: .preview
            ))
        }
        func says(_ labels: [String], _ text: String) -> Bool {
            labels.contains { $0.contains(text) }
        }

        let all = try await findings(clash: own("A1", from: 0), offer: .sample(id: "V4"))
        XCTAssertEqual([says(all, link), says(all, hold), says(all, join)], [true, false, false], "\(all)")
        let held = try await findings(clash: nil, offer: .sample(id: "V4"))
        XCTAssertEqual([says(held, link), says(held, hold), says(held, join)], [false, true, false], "\(held)")
        let joined = try await findings(clash: nil, offer: nil)
        XCTAssertEqual([says(joined, link), says(joined, hold), says(joined, join)], [false, false, true], "\(joined)")
    }

    /// *See N open jobs* is the Available badge, the server's total, and like the badge it is not drawn at none
    /// or before the total is known.
    func testSeeOpenJobsIsTheServersTotalAndIsHiddenAtNone() async throws {
        var open = OrderListItem.sample(id: "V5", status: ._0, cleaningDateTime: start.addingTimeInterval(7200))
        open.estimatedTime = 60
        let now = start.addingTimeInterval(-3600)
        func offers(_ labels: [String], _ count: Int) -> Bool {
            labels.contains { $0.contains(HostedSpeech.plain(L10n.Orders.seeOpen(count))) }
        }
        let week = try await HostedSpeech.labels(
            of: OpenWeek(now: now, open: [open], own: [], total: 137, onBrowseJobs: {})
        )
        XCTAssertTrue(offers(week, 137), "\(week)")
        let none = try await HostedSpeech.labels(of: OpenWeek(now: now, open: [], own: [], total: 0, onBrowseJobs: {}))
        XCTAssertFalse(offers(none, 0), "\(none)")
        let unknown = try await HostedSpeech.labels(
            of: OpenWeek(now: now, open: [open], own: [], total: nil, onBrowseJobs: {})
        )
        XCTAssertFalse(offers(unknown, 1), "\(unknown)")

        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: 6)
        item.isAssignedToCurrentUser = true
        let cancelled = try OrderDetail(item)
        let page = try await HostedSpeech.labels(of: VStack {
            CancelledSections(order: cancelled, now: now, ownRows: [], openRows: [], openTotal: 0, onBrowseJobs: {})
        })
        XCTAssertFalse(offers(page, 0), "\(page)")
        let counted = try await HostedSpeech.labels(of: VStack {
            CancelledSections(
                order: cancelled, now: now, ownRows: [], openRows: [open], openTotal: 12, onBrowseJobs: {}
            )
        })
        XCTAssertTrue(offers(counted, 12), "\(counted)")
    }

    /// Like the badge, *See N open jobs* leaves out a loaded open row whose slot has ended.
    func testSeeOpenJobsLeavesOutAnOpenRowWhoseSlotHasEnded() async throws {
        var ended = OrderListItem.sample(id: "V1", status: ._0, cleaningDateTime: start.addingTimeInterval(-7200))
        ended.estimatedTime = 60
        var open = OrderListItem.sample(id: "V5", status: ._0, cleaningDateTime: start.addingTimeInterval(7200))
        open.estimatedTime = 60
        func offers(_ labels: [String], _ count: Int) -> Bool {
            labels.contains { $0.contains(HostedSpeech.plain(L10n.Orders.seeOpen(count))) }
        }
        let week = try await HostedSpeech.labels(
            of: OpenWeek(now: start, open: [ended], own: [], total: 1, onBrowseJobs: {})
        )
        XCTAssertFalse(offers(week, 1), "\(week)")

        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: 6)
        item.isAssignedToCurrentUser = true
        let cancelled = try OrderDetail(item)
        let page = try await HostedSpeech.labels(of: VStack {
            CancelledSections(
                order: cancelled, now: start, ownRows: [], openRows: [ended, open], openTotal: 2, onBrowseJobs: {}
            )
        })
        XCTAssertTrue(offers(page, 1), "\(page)")
        XCTAssertFalse(offers(page, 2), "\(page)")
    }
}

/// Late and not set off: the set-off slide stays the move, and from the start the start is offered under it.
@MainActor
final class LateStartTests: XCTestCase {
    func testTheStartIsOfferedUnderTheSetOffOnceTheStartHasPassed() async {
        let client = FakePartnerOrderClient()
        let staleness = OrdersStaleness()
        let start = Date(timeIntervalSince1970: 1_786_000_000)
        var clock = start.addingTimeInterval(-600)
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: 2)
        item.isAssignedToCurrentUser = true
        item.cleaningDateTime = start
        item.estimatedTime = 120
        client.byIdResult = .success(item)
        let vm = OrderDetailViewModel(
            orderId: "A1",
            client: client,
            staleness: staleness,
            snackbar: SnackbarController(),
            pendingOffers: PendingOffersStore(client: client, ordersStaleness: staleness),
            now: { clock }
        )
        await vm.load()

        XCTAssertEqual(vm.primaryAction, .notifyOnTheWay)
        XCTAssertFalse(vm.offersStartFromConfirmed, "ten minutes before the start")
        clock = start.addingTimeInterval(600)
        XCTAssertEqual(vm.primaryAction, .notifyOnTheWay)
        XCTAssertTrue(vm.offersStartFromConfirmed, "ten minutes after it")
        await vm.dispatch(.start)
        XCTAssertEqual(client.commands.map(\.name), ["start"], "the server takes a start from Confirmed")
    }

    /// *Already there? Start cleaning* asks first, in a sheet: the start slide and Cancel over the page, the set-off
    /// slide still pinned behind it. Cancel sends nothing; the sheet's slide sends the start and closes it.
    func testAlreadyThereAsksInASheetAndKeepsTheSetOffSlidePinned() async throws {
        var sent: [OrderPrimaryAction] = []
        let hosted = HostedSpeech(
            StickyActionFooter(
                action: .notifyOnTheWay,
                inFlightAction: nil,
                onConfirm: { sent.append($0) },
                offersStartFromConfirmed: true
            ),
            size: CGSize(width: 393, height: 852)
        )
        defer { hosted.close() }
        try await hosted.settle()
        let setOff = HostedSpeech.plain(L10n.Orders.swipeToNotifyOnTheWay)
        let start = HostedSpeech.plain(L10n.Orders.slideToStart)

        XCTAssertTrue(hosted.activate(L10n.Orders.lateAlreadyThere), "\(hosted.labels)")
        try await hosted.settle(14)
        XCTAssertTrue(hosted.labels.contains(setOff), "the set-off slide stays pinned: \(hosted.labels)")
        XCTAssertTrue(hosted.labels.contains(start), "the start slide asks in the sheet: \(hosted.labels)")

        XCTAssertTrue(hosted.activate(L10n.cancel), "\(hosted.labels)")
        try await hosted.settle(14)
        XCTAssertFalse(hosted.labels.contains(start), "\(hosted.labels)")
        XCTAssertTrue(hosted.labels.contains(setOff))
        XCTAssertTrue(sent.isEmpty)

        XCTAssertTrue(hosted.activate(L10n.Orders.lateAlreadyThere), "\(hosted.labels)")
        try await hosted.settle(14)
        XCTAssertTrue(hosted.activate(L10n.Orders.slideToStart), "\(hosted.labels)")
        try await hosted.settle(14)
        XCTAssertEqual(sent, [.start])
        XCTAssertFalse(hosted.labels.contains(start), "the sheet closes on the start: \(hosted.labels)")
    }

    /// While the start asked for in the sheet is sent, the footer says so in place of the set-off slide.
    func testWhileTheStartIsSentTheFooterShowsItBusyNotTheSetOffSlide() async throws {
        let labels = try await HostedSpeech.labels(
            of: StickyActionFooter(
                action: .notifyOnTheWay,
                inFlightAction: .start,
                onConfirm: { _ in },
                offersStartFromConfirmed: true
            ),
            size: CGSize(width: 393, height: 852)
        )

        XCTAssertTrue(labels.contains(HostedSpeech.plain(L10n.Orders.startingOrder)), "\(labels)")
        XCTAssertFalse(labels.contains(HostedSpeech.plain(L10n.Orders.swipeToNotifyOnTheWay)), "\(labels)")
        XCTAssertFalse(labels.contains(HostedSpeech.plain(L10n.Orders.lateAlreadyThere)), "\(labels)")
    }
}
