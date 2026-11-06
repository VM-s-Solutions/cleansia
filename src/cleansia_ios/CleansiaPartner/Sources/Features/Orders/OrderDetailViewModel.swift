import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

/// Per-action discriminator so individual lifecycle buttons can show their own
/// spinner (the `OrderAction` parity).
enum OrderAction: Equatable {
    case take
    case acceptContract
    case notifyOnTheWay
    case start
    case markCashCollected
    case reportCashNotPaid
    case markDone
    case complete
    case declineOffer
    case reportLockout
    case drop

    /// Only the two reservation actions earn platform-owned framing; every other refusal on the detail
    /// reaches the snackbar exactly as it always did.
    var refusalKind: OfferRefusal.Kind? {
        switch self {
        case .take: .confirm
        case .declineOffer: .release
        case .acceptContract, .notifyOnTheWay, .start, .markCashCollected, .reportCashNotPaid, .markDone, .complete,
             .reportLockout, .drop: nil
        }
    }

    var mutation: OrdersMutation {
        switch self {
        case .take: .takeOrder
        case .acceptContract: .acceptWorkContract
        case .notifyOnTheWay: .notifyOnTheWay
        case .start: .startOrder
        case .markCashCollected: .markCashCollected
        case .reportCashNotPaid: .completeOrder
        case .markDone: .markMyPartDone
        case .complete: .completeOrder
        case .declineOffer: .declinePreferredOffer
        case .reportLockout: .reportLockout
        case .drop: .dropOrder
        }
    }

    /// Take stays silent — the action visibly flips. The slide transitions
    /// confirm out loud: their only passive cue is a quiet label swap, which
    /// cleaners read as a failed swipe. One mapping for the list AND the detail.
    var successFeedback: String? {
        switch self {
        case .notifyOnTheWay: L10n.Orders.customerNotifiedOnTheWay
        case .start: L10n.Orders.orderStartedToast
        case .markCashCollected: L10n.Orders.cashCollectedToast
        case .reportCashNotPaid: L10n.Orders.cashNotPaidReportedToast
        case .complete: L10n.Orders.orderCompletedToast
        case .declineOffer: L10n.Offers.declinedToast
        case .reportLockout: L10n.Orders.lockoutReportedToast
        // Which sentence depends on what the server says of the crew.
        case .take, .acceptContract, .markDone, .drop: nil
        }
    }
}

@MainActor
final class OrderDetailViewModel: ViewModel {
    @Published private(set) var state: UiState<OrderDetail> = .loading
    @Published private(set) var actionState: ActionState = .idle
    @Published private(set) var inFlightAction: OrderAction?
    /// The reservation held for this cleaner on this order, if there is one. The partner order DTO
    /// carries no reservation block — that field is customer-only, so no cleaner ever learns an order
    /// was reserved for someone else — so the disclosure is composed from the cleaner's own offers.
    /// Absent means an ordinary job, which is exactly right for the short-lead band, where the push
    /// fires but nothing is withheld.
    @Published private(set) var preferredOffer: PendingOfferItem?
    /// The action that produced the current `actionState.error`. It survives into the error precisely
    /// so a failure to RELEASE cannot wear the framing written for a failure to TAKE. Written only on
    /// failure and readable only through `refusal`, which is itself gated on the error — so a value
    /// left over from a previous attempt cannot be observed and never needs clearing.
    @Published private var refusedAction: OrderAction?
    /// The contract sheet the screen is showing, if any: a take, a standalone acceptance or a read.
    @Published private(set) var contractRequest: WorkContractRequest?
    /// The Job taken panel's facts while the sheet's take stays on screen: set by a successful take, its strip waiting
    /// until the order is read back, and gone with the sheet.
    @Published private(set) var takenJob: TakenJob?
    /// The signed-in cleaner's own id, needed to pair their acceptance with their crew entry. Resolved
    /// alongside the fetch and kept; a resolve that failed is asked again on the next load. Nil until
    /// it is, when the standing reads as none.
    @Published private(set) var myEmployeeId: String?
    /// Her own Active rows, for a job she has not taken or one that was cancelled: the board's when it holds them
    /// whole, else read here. Nil while the job needs none.
    @Published private(set) var ownRows: UiState<[OrderListItem]>?
    /// What the removal notice opens once the job is no longer hers: the page in place of the job.
    @Published private(set) var removal: RemovalPage?
    /// Fires once the cleaner is off the job, for the screen to leave it.
    let left = PassthroughSubject<Void, Never>()
    /// The board's row for this order, looked up once when the screen opens. None from a push, the Dashboard or a
    /// cold start: every entry carries only the id, so the store is the one place a row can come from.
    let boardRow: OrderListItem?
    let now: () -> Date

    private let orderId: String
    private let client: PartnerOrderClient
    private let staleness: OrdersStaleness
    private let snackbar: SnackbarController
    private let pendingOffers: PendingOffersStore
    private let boardStore: OrdersBoardStore?
    private let showRemovalReason: Bool
    /// The removal read answered: the reason, or none on record. A read that failed is no answer, and is asked again.
    private var removalAnswered = false
    private var removalReason: String?

    /// The removal page: the administrator's reason (none when the server has no removal row: a cover handed
    /// over), the job as the board last held it, and her next jobs and the open ones, each read when the store
    /// holds none.
    struct RemovalPage {
        var reason: String?
        var job: OrderListItem?
        var nextJobs: UiState<[OrderListItem]> = .loading
        var openJobs: UiState<[OrderListItem]> = .loading
        var openTotal: Int?
    }

    init(
        orderId: String,
        client: PartnerOrderClient,
        staleness: OrdersStaleness,
        snackbar: SnackbarController,
        pendingOffers: PendingOffersStore,
        boardStore: OrdersBoardStore? = nil,
        showRemovalReason: Bool = false,
        now: @escaping () -> Date = { Date() }
    ) {
        self.orderId = orderId
        self.client = client
        self.staleness = staleness
        self.snackbar = snackbar
        self.pendingOffers = pendingOffers
        self.boardStore = boardStore
        self.showRemovalReason = showRemovalReason
        self.now = now
        boardRow = boardStore?.row(for: orderId)
        super.init()
        pendingOffers.$offers
            .map { offers in offers.first { $0.id == orderId } }
            .assign(to: &$preferredOffer)
    }

    /// Re-read on every state change, never decided once at screen-open: the cleaner can take the job
    /// with the detail already in front of them, and the rails appear the moment they do.
    var canReadPhotos: Bool {
        state.loadedValue?.readsPhotos == true
    }

    /// The board's distance band: the detail's own DTO carries none, so a job with no board row has no band.
    var band: HomeDistanceBand? {
        boardRow?.homeDistanceBand
    }

    /// The board's open rows, for "Also open that day" and the week strip; nil when it has not loaded them.
    var storedOpenRows: [OrderListItem]? {
        boardStore?.available
    }

    /// The server's count of open jobs beside the board's open rows, for *See N open jobs*.
    var storedOpenTotal: Int? {
        boardStore?.availableTotal
    }

    /// Her own rows from the board alone: what a page that could not load its job still holds.
    var storedOwnRows: [OrderListItem]? {
        boardStore?.own
    }

    /// The board's row is one of her own jobs: drawn as hers until the job says otherwise.
    func holdsAsHers(_ row: OrderListItem) -> Bool {
        boardStore?.own?.contains { $0.id == row.id } == true
    }

    /// The page drawn from the board's row while the job loads, or when the rest of it failed; none once the server
    /// refuses the job, whatever the entry, so a job she was taken off does not stay on screen as hers.
    var rowDetail: OrderDetail? {
        guard !isRefused else { return nil }
        return boardRow.flatMap { OrderDetail(row: $0, mine: holdsAsHers($0)) }
    }

    /// `order.not_found`: the job is gone from a push opened late, and retrying cannot change the answer. Opened from
    /// the removal notice the refusal is the removal page's once the removal read has answered, and until then the
    /// ordinary failure, whose Retry asks again.
    var isGone: Bool {
        !showRemovalReason && isRefused
    }

    private var isRefused: Bool {
        if case let .error(error) = state { return error.code == Self.notFound }
        return false
    }

    private static let notFound = "order.not_found"

    /// The own job this one would overlap, once her rows have answered.
    var clashingJob: OrderListItem? {
        guard let order = state.loadedValue, !order.isAssignedToCurrentUser, let start = order.cleaningDateTime,
              let end = order.bookedEnd, let own = ownRows?.loadedValue
        else { return nil }
        return OrdersBoardRules.clash(start: start, end: end, own: own)
    }

    /// The caller's own acceptance, paired through their crew entry — the line, the banner, or nothing.
    var contractStanding: WorkContractStanding {
        state.loadedValue?.workContractStanding(myEmployeeId: myEmployeeId) ?? .none
    }

    /// The one valid primary action for the loaded order (the shared machine).
    var primaryAction: OrderPrimaryAction {
        guard let order = state.loadedValue else { return .none }
        let clash = clashingJob.flatMap { mine in
            mine.cleaningDateTime.flatMap { start in
                OrdersBoardRules.bookedEnd(mine).map { OrderPrimaryAction.Clash(start: start, end: $0) }
            }
        }
        return OrderPrimaryAction.action(
            for: order.status,
            isMine: order.isAssignedToCurrentUser,
            hasAfterPhotos: order.hasAfterPhotos,
            isCashPayment: order.payment.isCash,
            isPaymentSettled: order.payment.isSettled,
            crew: order.crewFinish(myEmployeeId: myEmployeeId),
            mateMarked: order.mateMarked(myEmployeeId),
            isCardNotCharged: order.payment.isCardNotCharged,
            start: order.cleaningDateTime,
            end: order.bookedEnd,
            now: now(),
            clash: clash,
            ownRowsPending: ownRows?.isLoading ?? Self.isTakeable(order)
        )
    }

    /// A job she could take: not hers, and still open to a seat.
    private static func isTakeable(_ order: OrderDetail) -> Bool {
        !order.isAssignedToCurrentUser && [._0, ._2, ._3, ._4].contains(order.status)
    }

    /// Late and not set off: the set-off slide stays the move, and the start is offered under it.
    var offersStartFromConfirmed: Bool {
        guard primaryAction == .notifyOnTheWay, let start = state.loadedValue?.cleaningDateTime else { return false }
        return now() >= start
    }

    /// Beside the cash collection, and only while the payment is still pending: a refunded or disputed payment
    /// is not a debt to report.
    var offersCashNotPaid: Bool {
        primaryAction == .collectCash && state.loadedValue?.payment.isPending == true
    }

    /// Only before the cleaner sets off and before the job's start, and never after they reported they cannot
    /// get in: leaving deletes the seat that report pays, and from its start a job nobody started is a no-show
    /// for the administrators, which leaving would put back on the board for the sweep to cancel.
    var canDropOrder: Bool {
        guard let order = state.loadedValue, order.isAssignedToCurrentUser, order.lockoutReportedAt == nil else {
            return false
        }
        if let start = order.cleaningDateTime, now() >= start { return false }
        switch order.status {
        case ._0, ._2: return true
        case ._1, ._3, ._4, ._5, ._6, .none: return false
        }
    }

    func load() async {
        async let identity: Void = resolveMyEmployeeId()
        async let removalRead: Void = readRemovalUnlessAnswered()
        await ensureOffersFresh()
        await fetch(quiet: showRemovalReason)
        await identity
        await removalRead
        await openRemovalPage()
        await readOwnRowsIfNeeded()
    }

    /// Silent on failure. `order.not_found` is an answer, no removal on record (a cover handed over); any other
    /// failure is none, so the next load asks again.
    private func readRemovalUnlessAnswered() async {
        guard showRemovalReason, !removalAnswered else { return }
        switch await client.getMyAssignmentRemovalReason(orderId: orderId) {
        case let .success(reason):
            removalReason = reason.isBlank ? nil : reason
        case let .failure(error):
            guard error.code == Self.notFound else { return }
        }
        removalAnswered = true
    }

    /// The notice opened a job she is off: `GetById` refused her and the removal read answered, or the job still
    /// shows but not as hers and a reason is on record. With no reason on record the page says the job left her
    /// schedule. A job that only failed to load is the ordinary failure, and a job hers again is shown.
    private func openRemovalPage() async {
        guard removal == nil, removalAnswered else { return }
        let notHers = removalReason != nil && state.loadedValue?.isAssignedToCurrentUser == false
        guard isRefused || notHers else { return }
        removal = RemovalPage(reason: removalReason, job: boardRow)
        await readRemovalLists(useStore: true)
    }

    /// "Check again": both lists read afresh.
    func checkAgain() async {
        await readRemovalLists(useStore: false)
    }

    private func readRemovalLists(useStore: Bool) async {
        let storedOwn = useStore ? boardStore?.own : nil
        let storedOpen = useStore ? boardStore?.available.map { OrderPage(rows: $0, total: boardStore?.availableTotal) }
            : nil
        if storedOwn == nil { removal?.nextJobs = .loading }
        if storedOpen == nil { removal?.openJobs = .loading }
        async let own = held(storedOwn, read: readOwnRows)
        async let open = held(storedOpen, read: readOpenPage)
        let (ownResult, openResult) = await (own, open)
        removal?.nextJobs = Self.uiState(ownResult.map { $0.filter { $0.id != self.orderId } })
        removal?.openJobs = Self.uiState(openResult.map(\.rows))
        removal?.openTotal = try? openResult.get().total
    }

    /// A job she has not taken waits for her own rows before Take, and a cancelled one shows what her day still
    /// holds: the board's rows when it has them whole, else her own read. A failed read leaves the clash to the
    /// server, as the board does.
    private func readOwnRowsIfNeeded() async {
        guard ownRows == nil, removal == nil, let order = state.loadedValue else { return }
        guard Self.isTakeable(order) || order.status == ._6 else { return }
        if let stored = boardStore?.own {
            ownRows = .loaded(stored)
            return
        }
        ownRows = .loading
        ownRows = await Self.uiState(readOwnRows())
    }

    private func readOwnRows() async -> ApiResult<[OrderListItem]> {
        guard let employeeId = await ownEmployeeId() else {
            return .failure(ApiError(WireContractViolation(field: "id"), endpoint: "Employee/GetCurrentEmployee"))
        }
        let query = OrdersQueryBuilder.query(tab: .active, ownEmployeeId: employeeId, period: .thisMonth)
        return await client.everyPage(query).map(\.rows)
    }

    /// The Available tab's first page, as the tab reads it, with the server's total.
    private func readOpenPage() async -> ApiResult<OrderPage> {
        guard let employeeId = await ownEmployeeId() else {
            return .failure(ApiError(WireContractViolation(field: "id"), endpoint: "Employee/GetCurrentEmployee"))
        }
        let query = OrdersQueryBuilder.query(tab: .available, ownEmployeeId: employeeId, period: .thisMonth)
        return await client.getPaged(query)
    }

    private func held<Value>(_ stored: Value?, read: () async -> ApiResult<Value>) async -> ApiResult<Value> {
        if let stored { return .success(stored) }
        return await read()
    }

    private func ownEmployeeId() async -> String? {
        await resolveMyEmployeeId()
        return myEmployeeId
    }

    private static func uiState(_ result: ApiResult<[OrderListItem]>) -> UiState<[OrderListItem]> {
        switch result {
        case let .success(rows): .loaded(rows)
        case let .failure(error): .error(error)
        }
    }

    /// Refusing the reservation from the job it belongs to; the same one write the offers list makes.
    func declinePreferredOffer() async {
        await run(.declineOffer) { await self.pendingOffers.decline(orderId: self.orderId) }
    }

    func dismissActionError() {
        if case .error = actionState { actionState = .idle }
    }

    /// A disclosed reservation is the only thing that earns the framing: with no hold this screen is
    /// the ordinary job it has always been, and its refusals stay on the snackbar.
    var refusal: OfferRefusal? {
        guard let reason = actionState.errorMessage,
              let kind = refusedAction?.refusalKind,
              let preferredOffer
        else { return nil }
        return OfferRefusal(kind: kind, displayOrderNumber: preferredOffer.displayOrderNumber, reason: reason)
    }

    private func ensureOffersFresh() async {
        guard pendingOffers.isStale else { return }
        _ = await pendingOffers.refresh()
    }

    private func resolveMyEmployeeId() async {
        guard myEmployeeId == nil, case let .success(id) = await client.currentEmployeeId() else { return }
        myEmployeeId = id
    }

    /// A start or a completion on a seat with no acceptance is not an error to read but a contract to
    /// accept: the same sheet opens, in accept mode, and the gesture springs back to be retried once
    /// the row exists.
    private func opensContractInstead(_ action: OrderAction, _ error: ApiError) -> Bool {
        (action == .start || action == .complete) && error.code == WorkContractErrorKey.acceptanceRequired
    }

    func dispatch(_ action: OrderPrimaryAction) async {
        switch action {
        case .take: take()
        case .notifyOnTheWay: await notifyOnTheWay()
        case .start: await start()
        case .collectCash: await markCashCollected()
        case .markDone: await markMyPartDone()
        case .complete, .completeJob: await complete()
        case .takeOverlaps, .takeWaiting, .setOffLocked, .awaitingCrew, .completeBlocked, .paymentNotConfirmed, .none:
            break
        }
    }

    /// Taking is accepting the contract: the take happens inside the sheet, on the swipe.
    func take() {
        contractRequest = .take(orderId: orderId)
    }

    func openContract(_ request: WorkContractRequest) {
        contractRequest = request
    }

    func dismissContract() {
        contractRequest = nil
        takenJob = nil
    }

    /// The sheet's verdict, handled exactly as the one-tap take used to be: a success refreshes the
    /// order, a refusal is framed (on a disclosed offer) or snackbarred and reconciled. A take keeps the sheet up on
    /// the Job taken panel while the order is read back behind it.
    func onWorkContractOutcome(_ outcome: WorkContractOutcome) async {
        let took = outcome == .taken(orderId: orderId)
        if took {
            takenJob = TakenJob(detail: shownOrder, readBack: false)
        } else {
            contractRequest = nil
        }
        let action: OrderAction = if case .accept = outcome.request { .acceptContract } else { .take }
        await run(action) { outcome.result }
        if took, contractRequest != nil {
            takenJob = TakenJob(detail: shownOrder, readBack: true)
        }
    }

    private var shownOrder: OrderDetail? {
        state.loadedValue ?? rowDetail
    }

    func notifyOnTheWay() async {
        await run(.notifyOnTheWay) { await self.client.notifyOnTheWay(orderId: self.orderId) }
    }

    func start() async {
        await run(.start) { await self.client.startOrder(orderId: self.orderId) }
    }

    func markCashCollected() async {
        await run(.markCashCollected) { await self.client.markCashCollected(orderId: self.orderId) }
    }

    func reportCashNotPaid() async {
        await run(.reportCashNotPaid) { await self.client.reportCashNotPaid(orderId: self.orderId) }
    }

    func markMyPartDone() async {
        await run(
            .markDone,
            confirmation: { crewDone in crewDone ? L10n.Orders.crewDoneToast : L10n.Orders.partMarkedDoneToast },
            { await self.client.markMyPartDone(orderId: self.orderId) }
        )
    }

    func complete() async {
        await run(.complete) {
            await self.client.completeOrder(orderId: self.orderId, actualMinutes: nil, notes: nil)
        }
    }

    func reportLockout(_ callAttempts: String) async {
        let note = callAttempts.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !note.isEmpty else { return }
        await run(.reportLockout) { await self.client.reportLockout(orderId: self.orderId, callAttempts: note) }
    }

    /// Unlike every other action, a success is not followed by a re-read: the server only shows a job its
    /// crew left as an open one to take, or not at all.
    func dropOrder() async {
        guard !actionState.isSubmitting else { return }
        // The screen draws the link from the clock only when something else redraws it, so a tap or a confirm
        // can arrive from the start on; the re-read draws it without the link.
        guard canDropOrder else {
            await fetch()
            return
        }
        actionState = .submitting
        inFlightAction = .drop

        switch await client.dropOrder(orderId: orderId) {
        case let .success(crewRemaining):
            snackbar.showSuccess(crewRemaining == 0 ? L10n.Orders.droppedNoCrewToast : L10n.Orders.droppedToast)
            staleness.invalidatePanes(for: OrderAction.drop.mutation)
            staleness.invalidateOrder(orderId)
            actionState = .idle
            inFlightAction = nil
            left.send()
        case let .failure(error):
            inFlightAction = nil
            refusedAction = .drop
            snackbar.showApiError(error)
            actionState = .error(ApiErrorLocalizer().message(for: error))
            staleness.invalidateOrder(orderId)
            await fetch()
        }
    }

    /// `quiet`: opened from the removal notice, a refusal is the page's answer, not a failure to toast.
    private func fetch(quiet: Bool = false) async {
        // O2: always acts on the orderId this VM was constructed with (the id
        // the list/detail response carried) — never a synthesized/echoed id.
        switch await client.getById(orderId: orderId) {
        case let .success(order):
            state = .loaded(order)
        case let .failure(error):
            if !quiet { snackbar.showApiError(error) }
            // Stay .loaded through a background-refetch failure — only the
            // first load (nothing loaded yet) flips to .error
            // (OrderDetailViewModel.kt:74-79 parity).
            if state.loadedValue == nil {
                state = .error(error)
            }
        }
    }

    private func run(_ action: OrderAction, _ block: @escaping () async -> ApiResult<Void>) async {
        await run(action, confirmation: { _ in action.successFeedback }, block)
    }

    private func run<Value>(
        _ action: OrderAction,
        confirmation: (Value) -> String?,
        _ block: @escaping () async -> ApiResult<Value>
    ) async {
        // Re-entry guard: one mutation in flight (O4).
        guard !actionState.isSubmitting else { return }
        actionState = .submitting
        inFlightAction = action

        switch await block() {
        case let .success(value):
            if let message = confirmation(value) {
                snackbar.showSuccess(message)
            }
            staleness.invalidatePanes(for: action.mutation)
            staleness.invalidateOrder(orderId)
            // Held through the refetch: the footer stays locked-busy until it
            // re-renders with the NEW action, so a second swipe in the gap
            // can't re-fire the already-applied transition.
            await fetch()
            actionState = .idle
            inFlightAction = nil
        case let .failure(error):
            // O4: clean reject — release immediately (retry springs back),
            // surface the message, keep the screen, refresh so a stale
            // "takeable" state corrects (e.g. already-taken order).
            inFlightAction = nil
            if opensContractInstead(action, error) {
                actionState = .idle
                contractRequest = .accept(orderId: orderId)
                return
            }
            refusedAction = action
            // Whatever the screen frames, it frames alone: a snackbar carrying the same bare reason
            // would land on top of the sentence that explains which promise broke.
            if !(action.refusalKind != nil && preferredOffer != nil) {
                snackbar.showApiError(error)
            }
            actionState = .error(ApiErrorLocalizer().message(for: error))
            staleness.invalidateOrder(orderId)
            await fetch()
        }
    }
}
