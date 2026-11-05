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
        photos: Bool = true,
        cashPending: Bool = false,
        mateMarked: Bool = false
    ) -> OrderPrimaryAction {
        OrderPrimaryAction.action(
            for: status,
            isMine: mine,
            hasAfterPhotos: photos,
            isCashPayment: cashPending,
            isPaymentSettled: !cashPending,
            crew: crew,
            mateMarked: mateMarked
        )
    }

    func testASoloJobKeepsTheSingleComplete() {
        XCTAssertEqual(action(.solo), .complete)
        XCTAssertEqual(action(.solo, photos: false), .completeBlocked(cashPending: false))
    }

    /// While no one on the crew has marked, her part is cleaning like a solo job's: the after photo comes first, then
    /// I'm done. Once a crew-mate has marked, I'm done is the move whatever the photos.
    func testOnACrewMyOpenPartWaitsForTheAfterPhotoUntilACrewMateHasMarked() {
        XCTAssertEqual(action(.myPartOpen, photos: false), .completeBlocked(cashPending: false))
        XCTAssertEqual(action(.myPartOpen, photos: false, cashPending: true), .completeBlocked(cashPending: true))
        XCTAssertEqual(action(.myPartOpen), .markDone)
        XCTAssertEqual(action(.myPartOpen, photos: false, mateMarked: true), .markDone)
        XCTAssertEqual(action(.myPartOpen, mateMarked: true), .markDone)
        XCTAssertEqual(OrderPrimaryAction.markDone.orderAction, .markDone)
    }

    /// The detail reads the crew-mate's mark off the seats the server sends.
    @MainActor
    func testTheDetailPinsTheAfterPhotoUntilTheCrewMateMarks() async {
        let client = FakePartnerOrderClient()
        let staleness = OrdersStaleness()
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: 4)
        item.isAssignedToCurrentUser = true
        item.hasAfterPhotos = false
        item.paymentType = Code(value: 2)
        item.paymentStatus = Code(value: 2)
        item.assignedEmployees = [
            AssignedEmployeeDto(id: "seat-0", employeeId: "emp-self"),
            AssignedEmployeeDto(id: "seat-1", employeeId: "emp-mate")
        ]
        client.byIdResult = .success(item)
        let vm = OrderDetailViewModel(
            orderId: "A2",
            client: client,
            staleness: staleness,
            snackbar: SnackbarController(),
            pendingOffers: PendingOffersStore(client: client, ordersStaleness: staleness)
        )
        await vm.load()
        XCTAssertEqual(vm.primaryAction, .completeBlocked(cashPending: false))

        item.assignedEmployees?[1].completionConfirmedAt = marked
        client.byIdResult = .success(item)
        await vm.load()
        XCTAssertEqual(vm.primaryAction, .markDone)
    }

    func testOnACrewWithMyPartMarkedNothingIsOfferedWhileTheOthersWork() {
        XCTAssertEqual(action(.waitingForCrew), .awaitingCrew)
        XCTAssertEqual(action(.waitingForCrew, photos: false), .awaitingCrew)
        XCTAssertNil(OrderPrimaryAction.awaitingCrew.orderAction)
    }

    /// The shared close sends the same completion as a job one cleaner works, and says the same words.
    func testOnceEveryoneHasMarkedTheSharedCompleteJobAppearsUnderTheAfterPhotoRule() {
        XCTAssertEqual(action(.everyoneDone), .completeJob)
        XCTAssertEqual(action(.everyoneDone, photos: false), .completeBlocked(cashPending: false))
        XCTAssertEqual(OrderPrimaryAction.completeJob.orderAction, .complete)
    }

    func testOnceEveryoneHasMarkedTheCashIsCollectedBeforeTheSharedCompleteJob() {
        XCTAssertEqual(action(.everyoneDone, cashPending: true), .collectCash)
        XCTAssertEqual(action(.everyoneDone, photos: false, cashPending: true), .completeBlocked(cashPending: true))
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
        let keys = [
            "order_mark_done",
            "marking_done",
            "order_part_marked_done_toast",
            "order_crew_done_toast",
            "slide_to_complete",
            "crew_both_parts",
            "crew_all_marked",
            "crew_mark_done_sub",
            "detail_waiting_for",
            "crew_done_at"
        ]
        let english = try table("en")
        // "I'm done" is the name the partner web shows; the crew completes with the solo words.
        XCTAssertEqual(english["order_mark_done"], "I'm done")
        XCTAssertEqual(english["slide_to_complete"], "Slide to complete")
        XCTAssertNil(english["order_crew_waiting"], "the wait is said by name now, not by this sentence")
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

    func testOnceEveryoneHasMarkedTheDetailOffersTheSharedComplete() async {
        let vm = await loadedVM(job(myPartDone: true, mateDone: true))

        XCTAssertEqual(vm.primaryAction, .completeJob)
    }

    func testCompleteJobSendsTheCompletion() async {
        let vm = await loadedVM(job(myPartDone: true, mateDone: true))

        await vm.dispatch(.completeJob)

        XCTAssertEqual(client.commands.map(\.name), ["complete"])
        XCTAssertEqual(client.commands.map(\.orderId), ["order-1"])
        XCTAssertEqual(snackbar.current?.text, L10n.Orders.orderCompletedToast)
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

    /// The last cleaner to mark is told the whole crew has: the shared Complete job is open to all of them now.
    func testTheLastMarkSaysEveryoneOnTheJobHasMarked() async {
        let vm = await loadedVM(job(myPartDone: false, mateDone: true))
        client.crewDoneAfterMark = true

        await vm.dispatch(.markDone)

        XCTAssertEqual(client.commands.map(\.name), ["markMyPartDone"])
        XCTAssertEqual(snackbar.current?.severity, .success)
        XCTAssertEqual(snackbar.current?.text, L10n.Orders.crewDoneToast)
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

/// What the footer draws for each crew step: the shared machine names the step, and this is where it gets its
/// words and its dispatch.
final class CrewCloseFooterTests: XCTestCase {
    private func footerSource() throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(
            contentsOf: root.appendingPathComponent("CleansiaPartner/Sources/Features/Orders/StickyActionFooter.swift"),
            encoding: .utf8
        )
    }

    func testEachCrewStepAndTheSoloCompleteDrawTheirOwnControl() throws {
        let footer = try footerSource().replacingOccurrences(of: "\\s+", with: " ", options: .regularExpression)

        XCTAssertTrue(footer.contains(
            "case .markDone: footer { slide( .markDone, idle: L10n.Orders.markDone, "
                + "busy: L10n.Orders.markingDone, detail: L10n.Orders.markDoneSub ) }"
        ), "I'm done, and on the slide under it that it marks her part")
        XCTAssertTrue(footer.contains("case .awaitingCrew: footer { CrewWait(mate: waitingFor) }"), "the wait")
        XCTAssertTrue(footer.contains(
            "case .completeJob: footer { slide(.completeJob, idle: L10n.Orders.slideToComplete, "
                + "busy: L10n.Orders.completingOrder) }"
        ), "the crew's Complete says the solo words")
        XCTAssertTrue(footer.contains(
            "case .complete: footer { slide(.complete, idle: L10n.Orders.slideToComplete, "
                + "busy: L10n.Orders.completingOrder) }"
        ), "the solo Complete")
    }

    /// The wait is the crew-mate by name: Call her while she has a phone, else say who the job waits for.
    func testTheWaitCallsTheCrewMateOrNamesHer() throws {
        let footer = try footerSource().replacingOccurrences(of: "\\s+", with: " ", options: .regularExpression)

        XCTAssertTrue(footer.contains("if let call = ContactActions.callURL(phone: mate?.phone) {"))
        XCTAssertTrue(footer.contains(
            "CleansiaOutlinedButton( \"\\(L10n.Orders.actionCall)\\(OrdersBoardRules.dot)\\(name)\","
        ), "Call · Lenka")
        XCTAssertTrue(footer.contains("Text(L10n.tie(L10n.Orders.waitingFor(name)))"), "Waiting for Lenka")
    }

    /// A crew-mate's number only ever becomes a `tel:` link: never printed, read aloud, selectable or copied.
    func testACrewMatesNumberIsOnlyEverDialled() throws {
        let footer = try footerSource()
        let crewFinish = try String(
            contentsOf: URL(fileURLWithPath: #filePath)
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .appendingPathComponent("Sources/Features/Orders/CrewFinish.swift"),
            encoding: .utf8
        )
        for source in [crewFinish, footer] {
            XCTAssertNil(source.range(of: #"Text\([^)\n]*phone"#, options: .regularExpression), "printed")
            XCTAssertNil(source.range(of: #"accessibility\w*\([^)\n]*phone"#, options: .regularExpression), "spoken")
            XCTAssertFalse(source.contains("textSelection"), "selectable")
            XCTAssertFalse(source.contains("UIPasteboard"), "copied")
        }
        XCTAssertTrue(footer.contains("if let call = ContactActions.callURL(phone: mate?.phone) {"))
    }

    /// The wait reads no per-locale value, so after an in-app language switch nothing it observes changes and
    /// it would keep the old language.
    func testTheWaitFollowsAnInAppLanguageSwitch() throws {
        let source = try footerSource()
        let start = try XCTUnwrap(source.range(of: "private struct CrewWait: View {"))
        let end = source.range(of: "\n}\n", range: start.upperBound ..< source.endIndex)?.upperBound ?? source.endIndex
        let hint = source[start.lowerBound ..< end]

        XCTAssertTrue(hint.contains("@Environment(\\.locale) private var locale"))
        XCTAssertTrue(hint.contains(".id(locale.identifier)"))
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

        let crewDone = try await LivePartnerOrderClient(staleness: shared).markMyPartDone(orderId: "order-1").get()

        XCTAssertFalse(crewDone)
        XCTAssertEqual(bodies.paths, ["/api/Order/MarkMyPartDone"])
        XCTAssertEqual(bodies.json(ofPath: "/api/Order/MarkMyPartDone")?.keys.sorted(), ["orderId"])
        XCTAssertEqual(bodies.json(ofPath: "/api/Order/MarkMyPartDone")?["orderId"] as? String, "order-1")
        XCTAssertFalse(shared.isDashboardStale)
    }

    /// The toast is chosen by whether the server says everyone on the job has now marked, so an answer
    /// without it is refused rather than read as a crew still working.
    func testTheMarkReadsWhetherTheCrewIsDoneOrRefusesAnAnswerWithout() async throws {
        let done = Data(#"{"orderId":"order-1","completionConfirmedAt":"2026-10-27T10:00:00Z","crewDone":true}"#.utf8)
        GeneratedWireSpine.install(recording: WireBodies()) { _ in (200, done) }
        let crewDone = try await LivePartnerOrderClient(staleness: OrdersStaleness()).markMyPartDone(orderId: "order-1")
            .get()
        XCTAssertTrue(crewDone)

        let unsaid = Data(#"{"orderId":"order-1","completionConfirmedAt":"2026-10-27T10:00:00Z"}"#.utf8)
        GeneratedWireSpine.install(recording: WireBodies()) { _ in (200, unsaid) }
        let result = await LivePartnerOrderClient(staleness: OrdersStaleness()).markMyPartDone(orderId: "order-1")
        XCTAssertEqual(result.apiErrorOrNil?.code, ApiError.wireContractCode)
    }
}
