import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

/// One tab's rows and how they got there. `loadedAt` is the last success; `error` the last failure, which with
/// no rows held is the error state and with rows held is the stale strip over them.
struct PaneState {
    var rows: [OrderListItem] = []
    var total: Int?
    var loadedAt: Date?
    var error: ApiError?
    var isPaging = false
    /// A failed *Load more*: the loaded rows stay, and the footer offers the page again.
    var pagingFailed = false
}

/// The Orders board: Available (paged, with the reserved jobs joined to their rows), Active (the cleaner's own,
/// to completion) and History (one period, to completion). The take itself happens in the contract sheet; this
/// keeps the board's side of it — the row leaves at once, the counts and offers refresh, and the taken row is
/// read back for the sheet's taken state.
@MainActor
final class OrdersListViewModel: ViewModel {
    @Published private(set) var tab: OrdersTab = .available
    @Published private(set) var panes: [OrdersPane: PaneState] = [:]
    @Published private(set) var refreshPhase: RefreshPhase = .idle
    @Published private(set) var availableSort: AvailableSort = .soonest
    @Published private(set) var completedPeriod: CompletedPeriod = .thisMonth
    @Published private(set) var cashHeld: [CashHeld] = []
    @Published private(set) var offers: [PendingOfferItem] = []
    /// Reserved jobs whose board row is not on the loaded page, read by id.
    @Published private(set) var offerRows: [String: OrderListItem] = [:]
    /// The hero's slide in flight, so a second swipe cannot fire the applied transition again.
    @Published private(set) var inFlightActionOrderId: String?
    @Published private(set) var contractRequest: WorkContractRequest?
    @Published private(set) var boardTake: BoardTake?
    @Published var declinePrompt: PendingOfferItem?
    @Published var offerRefusal: OfferRefusal?
    /// The first card a *Load more* appended, for VoiceOver focus.
    @Published private(set) var firstAppendedId: String?
    /// The card that took a taken row's place, for VoiceOver focus once the sheet closes.
    @Published private(set) var focusAfterTake: String?
    /// A take left no card: VoiceOver goes to the switcher's Available segment instead.
    @Published private(set) var focusSwitcherAfterTake = false
    /// The job in progress as the detail reads it, by order id, so the hero names the same step.
    @Published private(set) var heroProgress: [String: OrdersBoardRules.RunFacts] = [:]
    /// Last month's completed jobs, read only while This month has none: History's reference block.
    @Published private(set) var referenceMonth: [OrderListItem]?

    let navigateToDetail = PassthroughSubject<String, Never>()
    let now: () -> Date

    let client: PartnerOrderClient
    let staleness: OrdersStaleness
    let snackbar: SnackbarController
    private let payroll: PartnerPayrollClient?
    private let pendingOffers: PendingOffersStore?
    private let boardStore: OrdersBoardStore?
    var ownEmployeeId: String?
    private var running: [OrdersPane: Task<Void, Never>] = [:]
    private var missingOfferRows: Set<String> = []
    private let sessionEpoch: Int

    init(
        client: PartnerOrderClient,
        staleness: OrdersStaleness,
        snackbar: SnackbarController,
        payroll: PartnerPayrollClient? = nil,
        pendingOffers: PendingOffersStore? = nil,
        boardStore: OrdersBoardStore? = nil,
        now: @escaping () -> Date = Date.init
    ) {
        self.client = client
        self.staleness = staleness
        self.snackbar = snackbar
        self.payroll = payroll
        self.pendingOffers = pendingOffers
        self.boardStore = boardStore
        self.now = now
        sessionEpoch = staleness.sessionEpoch
        super.init()
        if let pendingOffers {
            offers = pendingOffers.offers
            pendingOffers.$offers.assign(to: &$offers)
        }
    }

    func pane(_ pane: OrdersPane) -> PaneState {
        panes[pane] ?? PaneState()
    }

    /// Loading until the first answer; the error state when no rows are held; else the rows.
    func state(of pane: OrdersPane) -> UiState<[OrderListItem]> {
        let state = self.pane(pane)
        if let error = state.error, state.rows.isEmpty { return .error(error) }
        return state.loadedAt == nil ? .loading : .loaded(state.rows)
    }

    var currentState: UiState<[OrderListItem]> {
        state(of: tab.pane)
    }

    // MARK: - Entry, tabs, pulls

    /// Available page 1, pending offers and cash held together, then the Active pane (its rows are what the
    /// board's overlap flags read). History waits for its tab.
    func onAppear() async {
        async let available: Void = ensureFresh(.available)
        async let offers: Void = refreshOffers(force: false)
        async let cash: Void = refreshCashHeld()
        _ = await (available, offers, cash)
        await ensureFresh(.active)
        if tab == .history { await ensureFresh(.history) }
    }

    func selectTab(_ newTab: OrdersTab) async {
        guard newTab != tab else { return }
        tab = newTab
        await ensureFresh(newTab.pane)
    }

    /// The pull, *Check again* and *Retry*: what a pull on the tab does. Available also re-reads Active (the
    /// overlap flags), the offers and the cash held.
    func userRefresh() async {
        refreshPhase = .userRefreshing
        defer { refreshPhase = .idle }
        switch tab {
        case .available:
            async let available: Void = load(.available)
            async let active: Void = load(.active)
            async let offers: Void = refreshOffers(force: true)
            async let cash: Void = refreshCashHeld()
            _ = await (available, active, offers, cash)
        case .active: await load(.active)
        case .history: await load(.history)
        }
    }

    func setAvailableSort(_ sort: AvailableSort) {
        availableSort = sort
    }

    /// A History fetch still running belongs to the old period: it lands nothing (`fetch` checks the period), and
    /// the new period is fetched once it ends.
    func setCompletedPeriod(_ period: CompletedPeriod) async {
        guard period != completedPeriod else { return }
        completedPeriod = period
        panes[.history] = PaneState()
        referenceMonth = nil
        if let stale = running[.history] { await stale.value }
        await load(.history)
    }

    func openDetail(_ orderId: String) {
        navigateToDetail.send(orderId)
    }

    /// Skips the network when this view model already shows the pane's rows and its watermark is warm. A warm
    /// watermark alone is not enough: `OrdersStaleness` outlives the view model, whose panes start empty.
    private func ensureFresh(_ pane: OrdersPane) async {
        guard self.pane(pane).loadedAt == nil || staleness.isPaneStale(pane) else { return }
        await load(pane)
    }

    /// One fetch per pane at a time; a second caller waits for the running one rather than returning early.
    func load(_ pane: OrdersPane) async {
        if let task = running[pane] {
            await task.value
            return
        }
        // The task clears its own slot, so whoever resumes first after it ends can start the next one.
        let task = Task {
            await self.fetch(pane)
            self.running[pane] = nil
        }
        running[pane] = task
        await task.value
    }

    private func fetch(_ pane: OrdersPane) async {
        if refreshPhase == .idle { refreshPhase = .backgroundRefreshing }
        defer { if refreshPhase == .backgroundRefreshing { refreshPhase = .idle } }
        let period = completedPeriod
        guard let employeeId = await resolveOwnEmployeeId(fresh: pane == .available) else {
            fail(pane, ApiError(WireContractViolation(field: "id"), endpoint: "Employee/GetCurrentEmployee"))
            return
        }
        let query = OrdersQueryBuilder.query(tab: tab(for: pane), ownEmployeeId: employeeId, period: period)
        let result = pane == .available ? await client.getPaged(query) : await client.everyPage(query)
        // The period changed while this page loaded: it would show under the wrong name.
        guard pane != .history || period == completedPeriod else { return }
        switch result {
        case let .success(page):
            panes[pane] = PaneState(rows: page.rows, total: page.total, loadedAt: now())
            staleness.markPaneFresh(pane)
            switch pane {
            case .available:
                liveStore?.publish(available: page.rows, total: page.total)
                await resolveReservedRows(reset: true)
            case .active:
                if page.rows.count >= (page.total ?? 0) { liveStore?.publish(own: page.rows) }
                await readHeroProgress()
            case .history:
                await readReferenceMonth(period: period, empty: page.rows.isEmpty)
            }
        case let .failure(error):
            fail(pane, error)
        }
    }

    /// The job in progress reads its detail and photos, so the hero says Finishing when the detail would.
    private func readHeroProgress() async {
        guard let hero, let orderId = hero.id, hero.status == ._4 else {
            heroProgress = [:]
            return
        }
        async let detail = client.getById(orderId: orderId)
        async let photos = client.getPhotos(orderId: orderId)
        let (detailResult, photosResult) = await (detail, photos)
        heroProgress = [orderId: OrdersBoardRules.runFacts(
            hero,
            detail: try? detailResult.get(),
            photos: (try? photosResult.get()) ?? [],
            ownEmployeeId: ownEmployeeId
        )].compactMapValues { $0 }
    }

    /// One extra page for last month, only while This month has no completed job.
    private func readReferenceMonth(period: CompletedPeriod, empty: Bool) async {
        guard empty, period == .thisMonth, let employeeId = ownEmployeeId else {
            referenceMonth = nil
            return
        }
        let query = OrdersQueryBuilder.query(tab: .history, ownEmployeeId: employeeId, period: .lastMonth)
        guard case let .success(page) = await client.everyPage(query), period == completedPeriod else { return }
        referenceMonth = page.rows
    }

    /// No snackbar for a list read: the error state or the stale strip says it, and is announced.
    private func fail(_ pane: OrdersPane, _ error: ApiError) {
        var state = self.pane(pane)
        state.error = error
        panes[pane] = state
        guard pane == tab.pane else { return }
        if state.rows.isEmpty {
            OrdersAccessibility.announce(L10n.Orders.loadError)
        } else if let loadedAt = state.loadedAt {
            OrdersAccessibility.announce(L10n.Orders.staleSince(
                OrdersBoardRules.staleWhen(loadedAt, now: now(), locale: CoreL10n.locale)
            ))
        }
    }

    /// O3: the caller's own id, the only one any pane is scoped by. Available re-reads it before each page; the
    /// cleaner's own panes reuse it. Unreadable or blank → nil, and the pane goes to its error state rather than
    /// sending an unscoped request (banner.md §3a).
    func resolveOwnEmployeeId(fresh: Bool) async -> String? {
        if !fresh, let ownEmployeeId { return ownEmployeeId }
        switch await client.currentEmployeeId() {
        case let .success(id) where !id.isBlank:
            ownEmployeeId = id
            return id
        default:
            return nil
        }
    }

    private func tab(for pane: OrdersPane) -> OrdersTab {
        switch pane {
        case .available: .available
        case .active: .active
        case .history: .history
        }
    }

    // MARK: - Available: paging, reserved jobs, cash held

    /// The next page of Available, appended once per id. A failure keeps the rows and offers the page again.
    func loadMore() async {
        var state = pane(.available)
        guard !state.isPaging, state.loadedAt != nil, state.rows.count < (state.total ?? 0) else { return }
        state.isPaging = true
        state.pagingFailed = false
        panes[.available] = state
        guard let employeeId = await resolveOwnEmployeeId(fresh: true) else { return pagingFailed() }
        var query = OrdersQueryBuilder.query(tab: .available, ownEmployeeId: employeeId, period: completedPeriod)
        query.offset = state.rows.count
        switch await client.getPaged(query) {
        case let .success(page):
            var next = pane(.available)
            let merged = OrdersBoardRules.appendPage(next.rows, page.rows)
            firstAppendedId = merged.dropFirst(next.rows.count).first?.id
            next.rows = merged
            next.total = page.total
            next.isPaging = false
            panes[.available] = next
            liveStore?.publish(available: merged, total: page.total)
        case .failure:
            pagingFailed()
        }
    }

    private func pagingFailed() {
        var state = pane(.available)
        state.isPaging = false
        state.pagingFailed = true
        panes[.available] = state
        OrdersAccessibility.announce(L10n.Orders.loadMoreFailed)
    }

    private func refreshOffers(force: Bool) async {
        guard let pendingOffers, force || pendingOffers.isStale else { return }
        _ = await pendingOffers.refresh()
        await resolveReservedRows()
    }

    private func refreshCashHeld() async {
        guard let payroll else { return }
        if case let .success(rows) = await payroll.getCashHeld() {
            cashHeld = rows
        } else {
            cashHeld = []
        }
    }

    /// A reservation whose row is not on the loaded page is read by id with the Available query; not found, it
    /// gets no card (the Dashboard still lists it).
    private func resolveReservedRows(reset: Bool = false) async {
        if reset { missingOfferRows = [] }
        let loaded = Set(pane(.available).rows.compactMap(\.id))
        let live = OrdersBoardRules.reservedCards(offers, rows: [], now: now()).compactMap(\.offer.id)
            .filter { !loaded.contains($0) }
        var rows = offerRows.filter { live.contains($0.key) }
        let wanted = live.filter { reset || (rows[$0] == nil && !missingOfferRows.contains($0)) }
        if !wanted.isEmpty, let employeeId = await resolveOwnEmployeeId(fresh: false) {
            for orderId in wanted {
                let query = OrdersQueryBuilder.reservedRow(orderId: orderId, ownEmployeeId: employeeId)
                if case let .success(page) = await client.getPaged(query), let row = page.rows.first {
                    rows[orderId] = row
                } else {
                    rows[orderId] = nil
                    missingOfferRows.insert(orderId)
                }
            }
        }
        offerRows = rows
    }

    func askToDecline(_ offer: PendingOfferItem) {
        declinePrompt = offer
    }

    func decline(_ offer: PendingOfferItem) async {
        declinePrompt = nil
        guard let pendingOffers, let orderId = offer.id else { return }
        switch await pendingOffers.decline(orderId: orderId) {
        case .success:
            snackbar.showSuccess(L10n.Offers.declinedToast)
            await load(.available)
        case let .failure(error):
            offerRefusal = OfferRefusal(
                kind: .release,
                displayOrderNumber: offer.displayOrderNumber,
                reason: ApiErrorLocalizer().message(for: error)
            )
        }
    }

    // MARK: - Taking from the board

    func take(_ row: OrderListItem) {
        guard contractRequest == nil, let orderId = row.id else { return }
        focusAfterTake = nil
        focusSwitcherAfterTake = false
        boardTake = BoardTake(snapshot: row)
        contractRequest = .take(orderId: orderId)
    }

    /// Back to jobs, Close, or a swipe down: the sheet leaves, and so does the board context.
    func dismissContract() {
        contractRequest = nil
        boardTake = nil
    }

    func openTakenJob() {
        guard let orderId = boardTake?.snapshot.id else { return dismissContract() }
        dismissContract()
        navigateToDetail.send(orderId)
    }

    /// The sheet stays open on both outcomes; the board reconciles behind it. A take ends a reservation it
    /// clashes with, so the offers are re-read after every outcome.
    func onWorkContractOutcome(_ outcome: WorkContractOutcome) async {
        guard case let .take(orderId) = outcome.request else { return }
        staleness.invalidatePanes(for: .takeOrder)
        switch outcome {
        case .taken:
            staleness.invalidateOrder(orderId)
            removeFromAvailable(orderId)
            async let taken: OrderListItem? = fetchTakenRow(orderId)
            async let refresh: Void = refreshAfterTake()
            let row = await taken
            boardTake?.taken = row
            boardTake?.takenLookupDone = true
            await refresh
        case .refused:
            await refreshAfterTake()
        case .accepted:
            break
        }
    }

    private func removeFromAvailable(_ orderId: String) {
        var state = pane(.available)
        let before = state.rows.count
        // Every card in screen order, reserved first: the one that takes the row's place, else the first, else none
        // (VoiceOver then goes to the switcher, §6.7).
        let cards = reserved.compactMap(\.row.id) + availableCards.compactMap(\.id)
        if let index = cards.firstIndex(of: orderId) {
            let rest = cards.filter { $0 != orderId }
            focusAfterTake = rest.indices.contains(index) ? rest[index] : rest.first
            focusSwitcherAfterTake = focusAfterTake == nil
        }
        state.rows.removeAll { $0.id == orderId }
        if state.rows.count < before, let total = state.total { state.total = max(total - 1, 0) }
        panes[.available] = state
        liveStore?.publish(available: state.rows, total: state.total)
    }

    /// The store outlives this board: a load still running when the session it was opened in ends lands nothing
    /// there, or the next cleaner's detail would read the last one's rows.
    private var liveStore: OrdersBoardStore? {
        staleness.sessionEpoch == sessionEpoch ? boardStore : nil
    }

    private func refreshAfterTake() async {
        async let available: Void = load(.available)
        async let active: Void = load(.active)
        async let offers: Void = refreshOffers(force: true)
        async let cash: Void = refreshCashHeld()
        _ = await (available, active, offers, cash)
    }

    /// D13: the street comes from the list read of the cleaner's own row, never the audited detail read.
    private func fetchTakenRow(_ orderId: String) async -> OrderListItem? {
        guard let employeeId = await resolveOwnEmployeeId(fresh: false) else { return nil }
        let query = OrdersQueryBuilder.takenRow(orderId: orderId, ownEmployeeId: employeeId)
        guard case let .success(page) = await client.getPaged(query) else { return nil }
        return page.rows.first { $0.id == orderId }
    }

    // MARK: - The hero's slide

    /// Set off (Confirmed) or start (On the way). In progress has no slide: *Open job* goes to the detail.
    func advance(_ row: OrderListItem) async {
        guard inFlightActionOrderId == nil, let orderId = row.id else { return }
        let action: OrderAction
        let result: ApiResult<Void>
        inFlightActionOrderId = orderId
        switch row.status {
        case ._2:
            action = .notifyOnTheWay
            result = await client.notifyOnTheWay(orderId: orderId)
        case ._3:
            action = .start
            result = await client.startOrder(orderId: orderId)
        default:
            inFlightActionOrderId = nil
            return
        }
        staleness.invalidatePanes(for: action.mutation)
        switch result {
        case .success:
            if let confirmation = action.successFeedback { snackbar.showSuccess(confirmation) }
            staleness.invalidateOrder(orderId)
            await load(.active)
        case let .failure(error):
            snackbar.showApiError(error)
            await load(.active)
        }
        inFlightActionOrderId = nil
    }
}

// MARK: - What the tabs show

extension OrdersListViewModel {
    /// The cleaner's own jobs, only when every one of them is loaded: an incomplete list would flag some
    /// overlaps and silently miss others, so it flags none and the server stays the guard.
    var ownJobsForClash: [OrderListItem] {
        let active = pane(.active)
        guard active.loadedAt != nil, active.error == nil, active.rows.count >= (active.total ?? 0) else { return [] }
        return active.rows
    }

    /// Live reservations whose board row is known, deadline first.
    var reserved: [(offer: PendingOfferItem, row: OrderListItem)] {
        let rows = pane(.available).rows + Array(offerRows.values)
        return OrdersBoardRules.reservedCards(offers, rows: rows, now: now()).compactMap { pair in
            pair.row.map { (pair.offer, $0) }
        }
    }

    /// Available's cards: no ended slot, no job already shown as reserved, in the chosen order.
    var availableCards: [OrderListItem] {
        let reservedIds = Set(reserved.compactMap(\.row.id))
        let rows = pane(.available).rows.filter { row in
            !OrdersBoardRules.isExpired(row, now: now()) && !reservedIds.contains(row.id ?? "")
        }
        return shownSort == .bestPay ? OrdersBoardRules.sortedByPay(rows) : rows
    }

    /// The order the list is in: Best pay only while it can be offered. The choice is kept for when it can again.
    var shownSort: AvailableSort {
        bestPayOffered ? availableSort : .soonest
    }

    var bestPayOffered: Bool {
        OrdersBoardRules.bestPayOffered(loaded: pane(.available).rows.count, total: pane(.available).total)
    }

    func clash(for row: OrderListItem) -> OrderListItem? {
        OrdersBoardRules.clashes(row, own: ownJobsForClash)
    }

    /// "Tomorrow: 1 job overlaps your 15:00–18:00 job", or the count over several days; nil with no clash.
    var overlapFinding: String? {
        OrdersBoardRules.overlapFinding(
            reserved.map(\.row) + availableCards,
            own: ownJobsForClash,
            now: now(),
            locale: CoreL10n.locale
        )
    }

    /// To the cent (D16), so in the app's language: the device's would put an English decimal point into a
    /// Ukrainian sentence.
    var cashPanel: OrdersBoardRules.CashPanel? {
        OrdersBoardRules.cashPanel(cashHeld, locale: CoreL10n.locale)
    }

    /// The hero's run: its row, and while it is in progress its detail and photos.
    var heroRun: OrdersBoardRules.RunFacts? {
        hero.flatMap { heroProgress[$0.id ?? ""] ?? OrdersBoardRules.runFacts($0) }
    }

    /// The open jobs the board has loaded for the next seven days, her own days marked; no count until they answer.
    var weekStrip: [OrdersBoardRules.WeekDay] {
        let available = pane(.available)
        let open = available.loadedAt.map { _ in available.rows.filter { !OrdersBoardRules.isExpired($0, now: now()) } }
        return OrdersBoardRules.weekStrip(now: now(), open: open, own: pane(.active).rows)
    }

    /// The soonest open job that does not collide with her own, as the board's row.
    var soonestOpen: OrderListItem? {
        pane(.available).rows
            .filter { !OrdersBoardRules.isExpired($0, now: now()) && clash(for: $0) == nil }
            .min { ($0.cleaningDateTime ?? .distantFuture) < ($1.cleaningDateTime ?? .distantFuture) }
    }

    var nextJobs: [OrderListItem] {
        pane(.active).rows
    }

    /// The board's currency, for a History period with no row to read one from: any row held, else the cash.
    var knownCurrencySymbol: String? {
        let rows = OrdersPane.allCases.flatMap { pane($0).rows }
        if let symbol = rows.lazy.compactMap({ OrdersFormat.symbolOf($0.currency) }).first { return symbol }
        return cashHeld.lazy.compactMap { EarningsFormat.currencySymbol($0.currencyCode) }.first
    }

    /// The cleaner's own job that matters now; read only from the Active pane, the one scoped to them.
    var hero: OrderListItem? {
        OrdersBoardRules.heroPick(pane(.active).rows)
    }

    var laterRows: [OrderListItem] {
        let heroId = hero?.id
        return pane(.active).rows.filter { $0.id != heroId }
    }

    /// The server's count, so a reserved job still counts in Available; less the loaded rows that have ended.
    func badgeCount(_ tab: OrdersTab) -> Int? {
        switch tab {
        case .available: OrdersBoardRules.openCount(
                total: pane(.available).total,
                rows: pane(.available).rows,
                now: now()
            )
        case .active: pane(.active).total
        case .history: nil
        }
    }
}
