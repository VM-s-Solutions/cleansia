import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import XCTest
@testable import CleansiaPartner

/// Paging, sorting, reserved jobs, the cash-held notice and the board's side of a take.
@MainActor
extension OrdersListViewModelTests {
    private func rows(_ ids: ClosedRange<Int>, prefix: String = "o") -> [OrderListItem] {
        ids.map { OrderListItem.sample(id: "\(prefix)\($0)") }
    }

    // MARK: paging

    func testLoadMoreAppendsTheNextPageOnceFromTheRowsHeld() async {
        client.pageResponder = { query in
            guard query.hasAvailableSpots == true else { return .success(OrderPage(rows: [], total: 0)) }
            let page = query.offset == 0 ? self.rows(1 ... 100) : self.rows(100 ... 137)
            return .success(OrderPage(rows: page, total: 137))
        }
        let vm = makeVM()
        await vm.onAppear()
        XCTAssertEqual(vm.pane(.available).rows.count, 100)
        await vm.loadMore()
        XCTAssertEqual(queries(.available).last?.offset, 100)
        XCTAssertEqual(vm.pane(.available).rows.count, 137, "o100 came back twice and is held once")
        XCTAssertEqual(vm.firstAppendedId, "o101")
        await vm.loadMore()
        XCTAssertEqual(queries(.available).count, 2, "nothing is left to load")
    }

    func testAFailedLoadMoreKeepsTheRowsAndOffersThePageAgain() async {
        client.pageResponder = { query in
            if query.offset > 0 { return .failure(ApiError(code: "network.unreachable")) }
            return .success(OrderPage(rows: self.rows(1 ... 100), total: 137))
        }
        let vm = makeVM()
        await vm.onAppear()
        await vm.loadMore()
        XCTAssertEqual(vm.pane(.available).rows.count, 100)
        XCTAssertTrue(vm.pane(.available).pagingFailed)
        XCTAssertFalse(vm.pane(.available).isPaging)
        XCTAssertNil(vm.pane(.available).error, "the list itself did not fail")
    }

    func testActiveIsPagedToTheTotalAndStopsOnAnEmptyPage() async {
        client.pageResponder = { query in
            switch query.offset {
            case 0: .success(OrderPage(rows: self.rows(1 ... 100, prefix: "a"), total: 150))
            case 100: .success(OrderPage(rows: self.rows(101 ... 150, prefix: "a"), total: 150))
            default: .success(OrderPage(rows: [], total: 150))
            }
        }
        let vm = makeVM()
        await vm.selectTab(.active)
        XCTAssertEqual(vm.pane(.active).rows.count, 150)
        XCTAssertEqual(queries(.active).map(\.offset), [0, 100])

        client = FakePartnerOrderClient()
        client.pageResponder = { query in
            .success(OrderPage(rows: query.offset == 0 ? self.rows(1 ... 100, prefix: "h") : [], total: 400))
        }
        let history = makeVM()
        await history.selectTab(.history)
        XCTAssertEqual(history.pane(.history).rows.count, 100)
        XCTAssertEqual(queries(.history).map(\.offset), [0, 100], "an empty page ends the loop")
    }

    // MARK: sort

    func testBestPayIsOfferedOnlyOnceEveryRowIsLoadedAndSortsWithoutARefetch() async {
        var low = OrderListItem.sample(id: "low", pay: 300)
        low.cleaningDateTime = Date(timeIntervalSince1970: 2_000_000_000)
        var high = OrderListItem.sample(id: "high", pay: 900)
        high.cleaningDateTime = Date(timeIntervalSince1970: 2_000_100_000)
        client.pagedResult = .success([low, high])
        client.pagedTotal = 3
        let vm = makeVM()
        await vm.onAppear()
        XCTAssertFalse(vm.bestPayOffered)
        vm.setAvailableSort(.bestPay)
        XCTAssertEqual(vm.availableCards.map(\.id), ["low", "high"], "pay is not sorted over a partial board")

        client.pagedTotal = 2
        await vm.userRefresh()
        let before = client.getPagedCallCount
        XCTAssertTrue(vm.bestPayOffered)
        vm.setAvailableSort(.bestPay)
        XCTAssertEqual(vm.availableCards.map(\.id), ["high", "low"])
        XCTAssertEqual(client.getPagedCallCount, before)
    }

    /// The plain label names the order the list is in: when Best pay can no longer be offered the list falls back to
    /// soonest first and says so, and the choice comes back once every row is loaded again.
    func testThePlainSortNamesTheOrderTheListIsIn() async {
        var total = 2
        client.pageResponder = { query in
            query.hasAvailableSpots == true
                ? .success(OrderPage(rows: [.sample(id: "low", pay: 300), .sample(id: "high", pay: 900)], total: total))
                : .success(OrderPage(rows: [], total: 0))
        }
        let vm = makeVM()
        await vm.onAppear()
        vm.setAvailableSort(.bestPay)
        XCTAssertEqual(vm.shownSort, .bestPay)

        total = 105
        await vm.userRefresh()
        XCTAssertFalse(vm.bestPayOffered)
        XCTAssertEqual(vm.shownSort, .soonest)
        XCTAssertEqual(vm.availableSort, .bestPay, "the choice is kept")

        total = 2
        await vm.userRefresh()
        XCTAssertEqual(vm.shownSort, .bestPay)
    }

    // MARK: reserved jobs

    func testAReservedJobOnThePageBecomesItsCardAndLeavesTheList() async {
        let now = Date()
        client.pagedResult = .success([.sample(id: "held"), .sample(id: "open")])
        client.pagedTotal = 2
        client.pendingOffersResult = .success([
            .sample(id: "held", respondByUtc: now.addingTimeInterval(3600)),
            .sample(id: "lapsed", respondByUtc: now.addingTimeInterval(-60))
        ])
        let vm = makeVM(offers: PendingOffersStore(client: client, ordersStaleness: staleness), now: now)
        await vm.onAppear()
        XCTAssertEqual(vm.reserved.map(\.row.id), ["held"])
        XCTAssertEqual(vm.availableCards.map(\.id), ["open"])
        XCTAssertEqual(vm.badgeCount(.available), 2, "the reserved job still counts")
        XCTAssertFalse(client.queries.contains { $0.id == "lapsed" }, "a lapsed offer is never looked up")
    }

    func testTheAvailableBadgeLeavesOutALoadedRowWhoseSlotHasEnded() async {
        let now = Date()
        var ended = OrderListItem.sample(id: "ended", status: ._0, cleaningDateTime: now.addingTimeInterval(-7200))
        ended.estimatedTime = 60
        var open = OrderListItem.sample(id: "open", status: ._0, cleaningDateTime: now.addingTimeInterval(7200))
        open.estimatedTime = 60
        client.pageResponder = { query in
            guard query.hasAvailableSpots == true else { return .success(OrderPage(rows: [], total: 0)) }
            return .success(OrderPage(rows: [ended], total: 1))
        }
        let vm = makeVM(now: now)
        await vm.onAppear()
        XCTAssertTrue(vm.availableCards.isEmpty, "the board says No open jobs right now")
        XCTAssertEqual(vm.badgeCount(.available), 0)
        XCTAssertNil(OrdersBoardRules.badge(vm.badgeCount(.available)), "so the badge is hidden")

        client.pageResponder = { query in
            guard query.hasAvailableSpots == true else { return .success(OrderPage(rows: [], total: 0)) }
            return .success(OrderPage(rows: [ended, open], total: 40))
        }
        await vm.userRefresh()
        XCTAssertEqual(vm.badgeCount(.available), 39, "the rows not loaded still count")
    }

    func testTheWeekStripPrintsNoCountUntilAvailableHasAnswered() async {
        let now = Date()
        let mine = OrderListItem.sample(id: "mine", cleaningDateTime: now.addingTimeInterval(3600))
        client.pageResponder = { query in
            query.hasAvailableSpots == true
                ? .success(OrderPage(rows: [], total: 0))
                : .success(OrderPage(rows: [mine], total: 1))
        }
        let vm = makeVM(now: now)
        await vm.selectTab(.active)
        XCTAssertEqual(vm.pane(.active).rows.map(\.id), ["mine"])
        XCTAssertEqual(vm.weekStrip.map(\.open), Array(repeating: nil, count: 7), "Available is still loading")
        let loading = stripHeight(vm.weekStrip)
        await vm.selectTab(.available)
        XCTAssertEqual(vm.weekStrip.map(\.open), Array(repeating: 0, count: 7), "a real zero once it has answered")
        XCTAssertEqual(stripHeight(vm.weekStrip), loading, accuracy: 0.5, "nothing under the strip moves")
        let counted = vm.weekStrip.map { OrdersBoardRules.WeekDay(day: $0.day, open: 2, mine: $0.mine) }
        XCTAssertEqual(stripHeight(counted), loading, accuracy: 0.5, "nor when the counts are bold")
    }

    private func stripHeight(_ cells: [OrdersBoardRules.WeekDay]) -> CGFloat {
        UIHostingController(rootView: OrdersWeekStrip(cells: cells))
            .sizeThatFits(in: CGSize(width: 393, height: CGFloat.greatestFiniteMagnitude))
            .height
    }

    func testAReservedJobOffThePageIsReadByIdAndANotFoundOneGetsNoCard() async throws {
        let now = Date()
        client.pendingOffersResult = .success([
            .sample(id: "far", respondByUtc: now.addingTimeInterval(3600)),
            .sample(id: "gone", respondByUtc: now.addingTimeInterval(7200))
        ])
        client.pageResponder = { query in
            switch query.id {
            case "far": .success(OrderPage(rows: [.sample(id: "far")], total: 1))
            case "gone": .success(OrderPage(rows: [], total: 0))
            default: .success(OrderPage(rows: [.sample(id: "open")], total: 1))
            }
        }
        let vm = makeVM(offers: PendingOffersStore(client: client, ordersStaleness: staleness), now: now)
        await vm.onAppear()
        let lookup = try XCTUnwrap(client.queries.first { $0.id == "far" })
        XCTAssertEqual(lookup.hasAvailableSpots, true)
        XCTAssertEqual(lookup.excludeEmployeeId, "emp-self")
        XCTAssertEqual(lookup.statuses, [._0, ._2, ._3, ._4])
        XCTAssertEqual(lookup.limit, 1)
        XCTAssertEqual(vm.reserved.map(\.row.id), ["far"])
    }

    func testADeclinedReservationToastsAndRefetchesTheBoard() async {
        let now = Date()
        client.pagedResult = .success([.sample(id: "held")])
        client.pendingOffersResult = .success([.sample(id: "held", respondByUtc: now.addingTimeInterval(3600))])
        let vm = makeVM(offers: PendingOffersStore(client: client, ordersStaleness: staleness), now: now)
        await vm.onAppear()
        let before = queries(.available).count
        await vm.decline(.sample(id: "held"))
        XCTAssertEqual(client.pendingOfferCommands.map(\.orderId), ["held"])
        XCTAssertEqual(snackbar.current?.text, L10n.Offers.declinedToast)
        XCTAssertEqual(queries(.available).count, before + 1)

        client.declineResult = .failure(ApiError(code: "order.offer_not_found"))
        await vm.decline(.sample(id: "held"))
        XCTAssertEqual(vm.offerRefusal?.kind, .release)
    }

    // MARK: cash held

    func testTheCashPanelComesFromTheFirstHiddenRowAndAFailureShowsNothing() async {
        let payroll = FakePayrollClient()
        payroll.cashHeldResult = .success([
            CashHeld(currencyCode: "CZK", amount: 3200, floatCap: 3000, cashJobsHidden: true)
        ])
        let vm = makeVM(payroll: payroll)
        await vm.onAppear()
        XCTAssertEqual(
            vm.cashPanel?.held,
            OrdersBoardRules.nonBreaking(OrdersFormat.money(3200, symbol: EarningsFormat.currencySymbol("CZK")))
        )
        XCTAssertEqual(vm.cashPanel?.over, true)
        payroll.cashHeldResult = .failure(ApiError(httpStatus: 403))
        await vm.userRefresh()
        XCTAssertNil(vm.cashPanel)
    }

    /// Active failed with no rows held: the error with Retry, then the open jobs of the next seven days the board still
    /// has (Retry is the way on, so no Check again), then the cash, as the compact notice over the cap.
    func testActiveFailedShowsTheErrorThenTheWeekThenTheCash() async throws {
        let payroll = FakePayrollClient()
        payroll.cashHeldResult = .success([
            CashHeld(currencyCode: "CZK", amount: 3200, floatCap: 3000, cashJobsHidden: true)
        ])
        var open = OrderListItem.sample(id: "open", status: ._0, cleaningDateTime: Date().addingTimeInterval(86400))
        open.estimatedTime = 60
        client.pageResponder = { query in
            query.hasAvailableSpots == true
                ? .success(OrderPage(rows: [open], total: 5))
                : .failure(ApiError(code: "network.unreachable"))
        }
        let vm = makeVM(payroll: payroll)
        await vm.onAppear()
        await vm.selectTab(.active)
        guard case .error = vm.state(of: .active) else { return XCTFail("Active failed") }

        let labels = try await HostedSpeech.labels(of: ActivePane(vm: vm))

        let limit = OrdersBoardRules.nonBreaking(OrdersFormat.money(3000, symbol: EarningsFormat.currencySymbol("CZK")))
        let order = [
            L10n.Orders.loadError, L10n.Orders.openNext7Days, L10n.Orders.seeOpen(5), L10n.Orders.cashHiddenBack(limit)
        ].map { wanted in labels.firstIndex { $0.contains(HostedSpeech.plain(L10n.tie(wanted))) } }
        XCTAssertFalse(order.contains(nil), "\(labels)")
        XCTAssertEqual(order.compactMap { $0 }, order.compactMap { $0 }.sorted(), "\(labels)")
        XCTAssertFalse(labels.contains { $0.contains(HostedSpeech.plain(L10n.Orders.checkAgain)) }, "\(labels)")
    }

    /// An empty History period still says "0 Kč": the currency comes from any row the board holds, else the cash.
    func testTheBoardKnowsItsCurrencyForAnEmptyPeriod() async {
        var open = OrderListItem.sample(id: "o1")
        open.currency = CurrencyListItem(code: "EUR", symbol: "€")
        client.pageResponder = { query in
            let rows = query.hasAvailableSpots == true ? [open] : []
            return .success(OrderPage(rows: rows, total: rows.count))
        }
        let vm = makeVM()
        await vm.onAppear()
        await vm.selectTab(.history)
        XCTAssertEqual(vm.knownCurrencySymbol, "€")

        client.pageResponder = { _ in .success(OrderPage(rows: [], total: 0)) }
        let payroll = FakePayrollClient()
        payroll.cashHeldResult = .success([
            CashHeld(currencyCode: "CZK", amount: 0, floatCap: 3000, cashJobsHidden: false)
        ])
        let cashOnly = makeVM(payroll: payroll)
        await cashOnly.onAppear()
        XCTAssertEqual(cashOnly.knownCurrencySymbol, EarningsFormat.currencySymbol("CZK"))
    }

    /// D16: the notice is exact to the cent, so its amounts read in the app's language, not the device's.
    func testTheCashNoticeAmountsFollowTheAppLanguage() async {
        let restore = CoreL10n.languageTag
        CoreL10n.apply(languageTag: "uk")
        defer { CoreL10n.apply(languageTag: restore) }
        let payroll = FakePayrollClient()
        payroll.cashHeldResult = .success([
            CashHeld(currencyCode: "CZK", amount: 3200.40, floatCap: 3000, cashJobsHidden: true)
        ])
        let vm = makeVM(payroll: payroll)
        await vm.onAppear()
        let symbol = EarningsFormat.currencySymbol("CZK")
        let ukrainian = Locale(identifier: "uk")
        XCTAssertEqual(
            vm.cashPanel?.held,
            OrdersBoardRules.nonBreaking(OrdersFormat.money(3200.40, symbol: symbol, locale: ukrainian))
        )
        XCTAssertEqual(
            vm.cashPanel?.limit,
            OrdersBoardRules.nonBreaking(OrdersFormat.money(3000, symbol: symbol, locale: ukrainian))
        )
    }

    // MARK: the board's side of a take

    func testATakeOpensTheSheetWithTheCardsRowAsItsContext() {
        let vm = makeVM()
        vm.take(.sample(id: "o1", pay: 780))
        XCTAssertEqual(vm.contractRequest, .take(orderId: "o1"))
        XCTAssertEqual(vm.boardTake?.snapshot.id, "o1")
        vm.take(.sample(id: "o2"))
        XCTAssertEqual(vm.contractRequest, .take(orderId: "o1"), "a second take waits for the first sheet")
    }

    /// The sheet stays on the taken job while the board drops the row, refreshes, and reads the row back as the
    /// cleaner's own — through the list, never the audited detail read (D13).
    func testATakenJobLeavesTheBoardAtOnceAndIsReadBackForTheSheet() async throws {
        client.pageResponder = { query in
            if query.id == "o1" {
                return .success(OrderPage(
                    rows: [.sample(id: "o1", customerAddress: "Křižíkova 8, Praha, 18600")],
                    total: 1
                ))
            }
            return .success(OrderPage(rows: [.sample(id: "o1"), .sample(id: "o2")], total: 2))
        }
        let vm = makeVM(offers: PendingOffersStore(client: client, ordersStaleness: staleness))
        await vm.onAppear()
        vm.take(.sample(id: "o1"))
        let offersBefore = client.pendingOffersCallCount

        await vm.onWorkContractOutcome(.taken(orderId: "o1"))

        XCTAssertEqual(vm.contractRequest, .take(orderId: "o1"), "the sheet stays on the taken state")
        let lookup = try XCTUnwrap(client.queries.first { $0.id == "o1" })
        XCTAssertEqual(lookup.employeeId, "emp-self")
        XCTAssertEqual(lookup.statuses, [._2, ._3, ._4])
        XCTAssertEqual(lookup.limit, 1)
        XCTAssertEqual(vm.boardTake?.taken?.customerAddress, "Křižíkova 8, Praha, 18600")
        XCTAssertTrue(vm.boardTake?.takenLookupDone == true)
        XCTAssertGreaterThan(client.pendingOffersCallCount, offersBefore, "a take can end a reservation")
        XCTAssertEqual(client.getByIdCallCount, 0)
        XCTAssertNil(snackbar.current)
    }

    func testTheTakenRowLeavesTheListBeforeAnyRefetchLands() async {
        client.pagedResult = .success([.sample(id: "o1"), .sample(id: "o2")])
        let vm = makeVM()
        await vm.onAppear()
        vm.take(.sample(id: "o1"))
        var heldDuringRefresh: [String?]?
        client.onGetPaged = {
            if heldDuringRefresh == nil { heldDuringRefresh = vm.pane(.available).rows.map(\.id) }
        }
        await vm.onWorkContractOutcome(.taken(orderId: "o1"))
        XCTAssertEqual(heldDuringRefresh, ["o2"])
        XCTAssertEqual(vm.focusAfterTake, "o2", "VoiceOver lands on the card that took its place")
    }

    /// §6.7: with no card left, VoiceOver goes to the switcher's Available segment; a sheet closed without a take
    /// moves focus nowhere, whatever an earlier take chose.
    func testTakingTheLastCardSendsFocusToTheSwitcherAndAPlainCloseMovesNothing() async {
        client.pagedResult = .success([.sample(id: "o1")])
        let vm = makeVM()
        await vm.onAppear()
        vm.take(.sample(id: "o1"))
        client.pagedResult = .success([])
        await vm.onWorkContractOutcome(.taken(orderId: "o1"))
        XCTAssertNil(vm.focusAfterTake)
        XCTAssertTrue(vm.focusSwitcherAfterTake, "no card is left, so the Available segment takes focus")
        vm.dismissContract()

        client.pagedResult = .success([.sample(id: "o2"), .sample(id: "o3")])
        await vm.userRefresh()
        vm.take(.sample(id: "o2"))
        await vm.onWorkContractOutcome(.taken(orderId: "o2"))
        XCTAssertEqual(vm.focusAfterTake, "o3")
        XCTAssertFalse(vm.focusSwitcherAfterTake)
        vm.dismissContract()

        vm.take(.sample(id: "o3"))
        vm.dismissContract()
        XCTAssertNil(vm.focusAfterTake, "an earlier take's focus target outlived its sheet")
        XCTAssertFalse(vm.focusSwitcherAfterTake)
    }

    func testARefusedTakeKeepsTheSheetRaisesNoSnackbarAndRefreshesTheBoard() async {
        let vm = makeVM()
        await vm.onAppear()
        vm.take(.sample(id: "o1"))
        let before = queries(.available).count
        await vm.onWorkContractOutcome(.refused(.take(orderId: "o1"), ApiError(code: "order.take.no_available_spots")))
        XCTAssertEqual(vm.contractRequest, .take(orderId: "o1"))
        XCTAssertNil(snackbar.current)
        XCTAssertEqual(queries(.available).count, before + 1)
        XCTAssertTrue(queries(.active).count >= 2)
    }

    func testBackToJobsClosesTheSheetAndOpenJobGoesToTheDetail() {
        let vm = makeVM()
        vm.take(.sample(id: "o1"))
        vm.dismissContract()
        XCTAssertNil(vm.contractRequest)
        XCTAssertNil(vm.boardTake)

        vm.take(.sample(id: "o2"))
        var opened: String?
        let token = vm.navigateToDetail.sink { opened = $0 }
        defer { token.cancel() }
        vm.openTakenJob()
        XCTAssertEqual(opened, "o2")
        XCTAssertNil(vm.contractRequest)
    }

    // MARK: the board's rows for the detail, the hero in progress, last month

    func testTheBoardHandsItsRowsToTheStoreTheDetailReads() async {
        client.pageResponder = { query in
            let rows: [OrderListItem] = query.hasAvailableSpots == true
                ? [.sample(id: "open", status: ._0)]
                : [.sample(id: "mine")]
            return .success(OrderPage(rows: rows, total: rows.count))
        }
        let store = OrdersBoardStore()
        let vm = makeVM(boardStore: store)
        XCTAssertNil(store.row(for: "open"))
        await vm.onAppear()
        XCTAssertEqual(store.available?.compactMap(\.id), ["open"])
        XCTAssertEqual(store.own?.compactMap(\.id), ["mine"])
        XCTAssertEqual(store.row(for: "mine")?.id, "mine")
        await store.clear()
        XCTAssertNil(store.available)
        XCTAssertNil(store.own)
    }

    /// The detail's *See N open jobs* is the tab's badge, so the store keeps the server's total with the rows.
    func testTheStoreKeepsTheServersOpenTotalWithItsRows() async {
        client.pageResponder = { query in
            query.hasAvailableSpots == true
                ? .success(OrderPage(rows: [.sample(id: "open", status: ._0)], total: 137))
                : .success(OrderPage(rows: [], total: 0))
        }
        let store = OrdersBoardStore()
        let vm = makeVM(boardStore: store)
        await vm.onAppear()
        XCTAssertEqual(store.availableTotal, 137)
        await store.clear()
        XCTAssertNil(store.availableTotal)
    }

    /// The store outlives the shell: a board load still running when the session is wiped lands nothing in it, so
    /// the next cleaner's detail never reads the last one's rows.
    func testALoadThatOutlivesTheSessionLeavesTheStoreEmpty() async throws {
        let container = try PartnerAppContainer(
            snackbar: SnackbarController(),
            apiBaseURL: XCTUnwrap(URL(string: "https://board-store.test"))
        )
        client.pageResponder = { query in
            let rows: [OrderListItem] = query.hasAvailableSpots == true
                ? [.sample(id: "open", status: ._0)]
                : [.sample(id: "mine")]
            return .success(OrderPage(rows: rows, total: rows.count))
        }
        client.suspendGetPaged = true
        let vm = OrdersListViewModel(
            client: client,
            staleness: container.ordersStaleness,
            snackbar: snackbar,
            boardStore: container.ordersBoardStore
        )
        let loading = Task { await vm.onAppear() }
        while client.getPagedCallCount == 0 {
            await Task.yield()
        }

        await container.sessionScopedCaches.clearAll()
        client.resumeGetPaged()
        await loading.value

        XCTAssertNil(container.ordersBoardStore.available)
        XCTAssertNil(container.ordersBoardStore.own)
    }

    func testAnIncompleteOwnListIsNotHandedOn() async {
        client.pageResponder = { query in
            let rows: [OrderListItem] = query.hasAvailableSpots == true || query.offset > 0 ? [] : [.sample(id: "mine")]
            return .success(OrderPage(rows: rows, total: query.hasAvailableSpots == true ? 0 : 5))
        }
        let store = OrdersBoardStore()
        let vm = makeVM(boardStore: store)
        await vm.onAppear()
        XCTAssertNil(store.own, "the clash check and the detail need every one of her jobs, or none")
    }

    /// The hero in progress reads its detail and photos, so it says Finishing once an after photo exists.
    func testTheHeroInProgressReadsItsDetailAndPhotos() async {
        let start = Date().addingTimeInterval(-3600)
        var mine = OrderListItem.sample(id: "mine", status: ._4, cleaningDateTime: start)
        mine.estimatedTime = 180
        client.pageResponder = { query in
            .success(OrderPage(rows: query.hasAvailableSpots == true ? [] : [mine], total: 1))
        }
        client.getPhotosResult = .success([OrderPhoto(id: "p", photoType: ._2, blobUrl: nil)])
        let vm = makeVM()
        await vm.selectTab(.active)
        XCTAssertEqual(client.getByIdCallCount, 1)
        XCTAssertEqual(client.getPhotosCallCount, 1)
        XCTAssertEqual(vm.heroRun.flatMap(OrdersBoardRules.runStep), .finish)

        mine.orderStatus = Code(value: 2)
        await vm.userRefresh()
        XCTAssertEqual(client.getByIdCallCount, 1, "a job not in progress reads no detail")
        XCTAssertEqual(vm.heroRun.flatMap(OrdersBoardRules.runStep), .setOff)
    }

    /// An empty period reads last month once, for the reference block; a period with jobs reads nothing more.
    func testAnEmptyPeriodReadsLastMonthAsItsReference() async {
        var october = OrderListItem.sample(id: "oct", status: ._5)
        october.estimatedTime = 120
        client.pageResponder = { query in
            guard query.statuses == [._5] else { return .success(OrderPage(rows: [], total: 0)) }
            let lastMonth = OrdersQueryBuilder.periodToDateRange(.lastMonth).to
            return .success(query.cleaningDateTo == lastMonth
                ? OrderPage(rows: [october], total: 1)
                : OrderPage(rows: [], total: 0))
        }
        let vm = makeVM()
        await vm.selectTab(.history)
        XCTAssertEqual(vm.referenceMonth?.compactMap(\.id), ["oct"])
        XCTAssertEqual(queries(.history).count, 2)

        await vm.setCompletedPeriod(.lastMonth)
        XCTAssertNil(vm.referenceMonth, "last month is never its own reference")
    }

    /// Only an empty This month reads last month, as Android: an empty week reads nothing more.
    func testAnEmptyWeekReadsNoReference() async {
        let october = OrderListItem.sample(id: "oct", status: ._5)
        client.pageResponder = { query in
            guard query.statuses == [._5] else { return .success(OrderPage(rows: [], total: 0)) }
            let lastMonth = OrdersQueryBuilder.periodToDateRange(.lastMonth).to
            return .success(query.cleaningDateTo == lastMonth
                ? OrderPage(rows: [october], total: 1)
                : OrderPage(rows: [], total: 0))
        }
        let vm = makeVM()
        await vm.selectTab(.history)
        XCTAssertEqual(vm.referenceMonth?.compactMap(\.id), ["oct"])

        await vm.setCompletedPeriod(.thisWeek)
        XCTAssertNil(vm.referenceMonth, "an empty week shows no reference")
        XCTAssertEqual(queries(.history).count, 3, "the month, its reference, then the week alone")
    }
}
