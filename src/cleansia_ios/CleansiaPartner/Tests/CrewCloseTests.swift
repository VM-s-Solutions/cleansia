import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The crew close (owner ruling 2026-10-26): on a job with two or more cleaners each marks their own part
/// done on their device, and once every cleaner still on it has, any of them completes it. A job one cleaner
/// works keeps the single Complete.
final class CrewCloseTests: XCTestCase {
    private let marked = Date(timeIntervalSince1970: 1_786_000_000)
    private let appBundle = Bundle(identifier: "cz.cleansia.partner") ?? .main

    private func order(_ seats: [(employeeId: String, done: Bool)]) throws -> OrderDetail {
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: 4)
        item.isAssignedToCurrentUser = true
        item.hasAfterPhotos = true
        item.assignedEmployees = seats.enumerated().map { index, seat in
            AssignedEmployeeDto(
                id: "seat-\(index)",
                employeeId: seat.employeeId,
                completionConfirmedAt: seat.done ? marked : nil
            )
        }
        return try OrderDetail(item)
    }

    private func standing(
        _ seats: [(employeeId: String, done: Bool)],
        myId: String? = "emp-self"
    ) throws -> CrewFinish {
        try order(seats).crewFinish(myEmployeeId: myId)
    }

    // MARK: - The seat

    func testASeatCarriesWhenItsCleanerMarkedTheirPartDone() throws {
        let detail = try order([("emp-self", true), ("emp-mate", false)])

        XCTAssertEqual(detail.seats.map(\.completionConfirmedAt), [marked, nil])
    }

    // MARK: - Where the crew stands

    func testOneCleanerOnTheJobIsSolo() throws {
        XCTAssertEqual(try standing([("emp-self", false)]), .solo)
        XCTAssertEqual(try standing([("emp-self", true)]), .solo)
    }

    func testOnACrewMyUnmarkedPartIsOpen() throws {
        XCTAssertEqual(try standing([("emp-self", false), ("emp-mate", false)]), .myPartOpen)
        XCTAssertEqual(try standing([("emp-self", false), ("emp-mate", true)]), .myPartOpen)
    }

    func testOnACrewWithMyPartMarkedTheOthersAreAwaited() throws {
        XCTAssertEqual(try standing([("emp-self", true), ("emp-mate", false)]), .waitingForCrew)
        XCTAssertEqual(try standing([("emp-a", false), ("emp-self", true), ("emp-b", true)]), .waitingForCrew)
    }

    func testOnACrewEveryPartMarkedIsEveryoneDoneWhoeverAsks() throws {
        XCTAssertEqual(try standing([("emp-self", true), ("emp-mate", true)]), .everyoneDone)
        XCTAssertEqual(try standing([("emp-self", true), ("emp-mate", true)], myId: nil), .everyoneDone)
    }

    /// Until the caller's own id is known their seat cannot be read; marking again keeps the first time, so
    /// the mark is what is safe to offer.
    func testWithMyIdUnknownAnUnfinishedCrewOffersTheMark() throws {
        XCTAssertEqual(try standing([("emp-self", true), ("emp-mate", false)], myId: nil), .myPartOpen)
    }

    // MARK: - The action

    private func action(
        _ crew: CrewFinish,
        status: OrderStatus? = ._4,
        mine: Bool = true,
        photos: Bool = true
    ) -> OrderPrimaryAction {
        OrderPrimaryAction.action(
            for: status,
            isMine: mine,
            hasAfterPhotos: photos,
            isCashPayment: false,
            isPaymentSettled: true,
            crew: crew
        )
    }

    func testASoloJobKeepsTheSingleComplete() {
        XCTAssertEqual(action(.solo), .complete)
        XCTAssertEqual(action(.solo, photos: false), .completeBlocked(cashPending: false))
    }

    /// The mark has no photo rule; the after photo is the shared Complete's.
    func testOnACrewMyOpenPartOffersImDoneWhateverThePhotos() {
        XCTAssertEqual(action(.myPartOpen), .markDone)
        XCTAssertEqual(action(.myPartOpen, photos: false), .markDone)
        XCTAssertEqual(OrderPrimaryAction.markDone.orderAction, .markDone)
    }

    func testOnACrewWithMyPartMarkedNothingIsOfferedWhileTheOthersWork() {
        XCTAssertEqual(action(.waitingForCrew), .awaitingCrew)
        XCTAssertEqual(action(.waitingForCrew, photos: false), .awaitingCrew)
        XCTAssertNil(OrderPrimaryAction.awaitingCrew.orderAction)
    }

    func testOnceEveryoneHasMarkedTheSharedCompleteAppearsUnderTheAfterPhotoRule() {
        XCTAssertEqual(action(.everyoneDone), .complete)
        XCTAssertEqual(action(.everyoneDone, photos: false), .completeBlocked(cashPending: false))
    }

    func testACleanerNotOnTheJobIsStillOfferedTheOpenSeat() {
        XCTAssertEqual(action(.myPartOpen, mine: false), .take)
    }

    func testTheCrewChangesNothingOutsideAJobInProgress() {
        for status in [OrderStatus._0, ._1, ._2, ._3, ._5, ._6] {
            for mine in [true, false] {
                XCTAssertEqual(
                    action(.myPartOpen, status: status, mine: mine),
                    action(.solo, status: status, mine: mine),
                    "\(status) mine: \(mine)"
                )
            }
        }
    }

    // MARK: - Copy

    func testTheCrewCloseCopyIsWrittenInAllFiveLanguages() throws {
        let keys = ["slide_to_mark_done", "marking_done", "order_part_marked_done_toast", "order_crew_waiting"]
        let english = try table("en")
        for language in ["en", "cs", "sk", "uk", "ru"] {
            let strings = try table(language)
            for key in keys {
                let value = try XCTUnwrap(strings[key], "\(key) missing from \(language).lproj")
                XCTAssertFalse(value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, "\(language) \(key)")
                if language != "en" {
                    XCTAssertNotEqual(value, english[key], "\(language).lproj left \(key) in English")
                }
            }
        }
    }

    private func table(_ language: String) throws -> [String: String] {
        let lproj = try XCTUnwrap(appBundle.url(forResource: language, withExtension: "lproj"), language)
        let url = lproj.appendingPathComponent("Localizable.strings")
        return try XCTUnwrap(NSDictionary(contentsOf: url) as? [String: String], language)
    }
}

@MainActor
final class CrewCloseDetailTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var staleness: OrdersStaleness!
    private var snackbar: SnackbarController!

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

    private func job(myPartDone: Bool, mateDone: Bool?) -> OrderItem {
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: 4)
        item.isAssignedToCurrentUser = true
        item.hasAfterPhotos = true
        let marked = Date(timeIntervalSince1970: 1_786_000_000)
        item.assignedEmployees = [
            AssignedEmployeeDto(id: "seat-1", employeeId: "emp-self", completionConfirmedAt: myPartDone ? marked : nil)
        ] + (mateDone.map {
            [AssignedEmployeeDto(id: "seat-2", employeeId: "emp-mate", completionConfirmedAt: $0 ? marked : nil)]
        } ?? [])
        return item
    }

    private func loadedVM(_ item: OrderItem) async -> OrderDetailViewModel {
        client.byIdResult = .success(item)
        let vm = makeVM()
        await vm.load()
        return vm
    }

    func testOnACrewJobTheDetailOffersImDone() async {
        let vm = await loadedVM(job(myPartDone: false, mateDone: false))

        XCTAssertEqual(vm.primaryAction, .markDone)
    }

    func testAfterMyMarkTheDetailWaitsForTheRestOfTheCrew() async {
        let vm = await loadedVM(job(myPartDone: true, mateDone: false))

        XCTAssertEqual(vm.primaryAction, .awaitingCrew)
    }

    func testOnceEveryoneHasMarkedTheDetailOffersComplete() async {
        let vm = await loadedVM(job(myPartDone: true, mateDone: true))

        XCTAssertEqual(vm.primaryAction, .complete)
    }

    func testAJobOneCleanerWorksStillCompletesInOneTap() async {
        let vm = await loadedVM(job(myPartDone: false, mateDone: nil))

        XCTAssertEqual(vm.primaryAction, .complete)
    }

    func testImDoneMarksMyPartSaysSoAndReReadsTheJob() async {
        let vm = await loadedVM(job(myPartDone: false, mateDone: false))
        let fetches = client.getByIdCallCount
        for pane in OrdersPane.allCases {
            staleness.markPaneFresh(pane)
        }

        await vm.dispatch(.markDone)

        XCTAssertEqual(client.commands.map(\.name), ["markMyPartDone"])
        XCTAssertEqual(client.commands.map(\.orderId), ["order-1"])
        XCTAssertEqual(snackbar.current?.severity, .success)
        XCTAssertEqual(snackbar.current?.text, L10n.Orders.partMarkedDoneToast)
        XCTAssertEqual(client.getByIdCallCount, fetches + 1)
        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertNil(vm.inFlightAction)
        // The job stays in progress, so no list changes.
        for pane in OrdersPane.allCases {
            XCTAssertFalse(staleness.isPaneStale(pane), "\(pane)")
        }
    }

    func testARefusedMarkShowsTheReasonAndReReadsTheJob() async {
        let vm = await loadedVM(job(myPartDone: false, mateDone: false))
        client.commandResult = .failure(ApiError(code: "order.not_in_progress", httpStatus: 400))
        let fetches = client.getByIdCallCount

        await vm.markMyPartDone()

        XCTAssertEqual(snackbar.current?.severity, .error)
        XCTAssertEqual(client.getByIdCallCount, fetches + 1)
        XCTAssertNil(vm.inFlightAction)
    }

    func testWaitingForTheCrewDispatchesNothing() async {
        let vm = await loadedVM(job(myPartDone: true, mateDone: false))

        await vm.dispatch(.awaitingCrew)

        XCTAssertTrue(client.commands.isEmpty)
    }
}

@MainActor
final class CrewCloseWireTests: XCTestCase {
    override func tearDown() {
        GenMockURLProtocol.handler = nil
        super.tearDown()
    }

    /// The mark names only the job; the seat is the session's. It changes nothing the dashboard counts, so
    /// the dashboard stays warm.
    func testImDoneSendsTheJobToMarkMyPartDoneAndLeavesTheDashboardWarm() async throws {
        let shared = OrdersStaleness()
        shared.markDashboardFresh()
        let bodies = WireBodies()
        let answer = Data(#"{"orderId":"order-1","completionConfirmedAt":"2026-10-27T10:00:00Z","crewDone":false}"#
            .utf8)
        GeneratedWireSpine.install(recording: bodies) { _ in (200, answer) }

        try await LivePartnerOrderClient(staleness: shared).markMyPartDone(orderId: "order-1").get()

        XCTAssertEqual(bodies.paths, ["/api/Order/MarkMyPartDone"])
        XCTAssertEqual(bodies.json(ofPath: "/api/Order/MarkMyPartDone")?.keys.sorted(), ["orderId"])
        XCTAssertEqual(bodies.json(ofPath: "/api/Order/MarkMyPartDone")?["orderId"] as? String, "order-1")
        XCTAssertFalse(shared.isDashboardStale)
    }
}
