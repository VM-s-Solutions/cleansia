import CleansiaCore
import CleansiaCustomerApi
import Combine
import Foundation

enum PhotosUiState {
    case idle
    case loading
    case loaded(OrderPhotos)
    case error
}

extension PhotosUiState {
    var loadedResponse: OrderPhotos? {
        if case let .loaded(response) = self { return response }
        return nil
    }
}

@MainActor
final class OrderDetailViewModel: ViewModel {
    @Published private(set) var state: UiState<CustomerOrderDetail> = .loading
    @Published private(set) var photos: PhotosUiState = .idle
    @Published private(set) var cancelState: ActionState = .idle
    @Published private(set) var cancellationQuote: UiState<CancellationQuote> = .loading
    @Published private(set) var reviewState: ActionState = .idle
    @Published private(set) var receiptState: ActionState = .idle
    @Published private(set) var confirmRecurringState: ActionState = .idle
    /// The confirmation was refused because the customer owes a company money; the screen offers Payments.
    @Published private(set) var owesMoney = false
    @Published private(set) var saveCard = false
    /// Nil until the account's consents are read for a visit awaiting confirmation; a failed read is `false`.
    @Published private(set) var alreadyConsented: Bool?
    @Published private(set) var termsAccepted = false
    @Published private(set) var hasMembership: Bool?
    @Published private(set) var markets: MarketState = .loading

    let cancelSucceeded = PassthroughSubject<OrderCancellation, Never>()
    let reviewSucceeded = PassthroughSubject<OrderReviewDto, Never>()
    let receiptReady = PassthroughSubject<URL, Never>()
    let recurringCardPayment = PassthroughSubject<PaymentSheetPresentation, Never>()

    private let orderId: String
    private let client: OrderClient
    private let repository: OrderRepository
    private let membershipRepository: MembershipRepository
    private let marketStore: MarketStore
    private let snackbar: SnackbarController
    private let eventBus: OrderEventBus
    private let paymentIntentClient: PaymentIntentClient
    private let consentClient: ConsentStatusClient
    private let liveActivity: OrderLiveActivitySyncing
    /// Re-reads the credit balance the Rewards and Profile screens show. Required, with no default: the
    /// balance moves here twice — a cancelled order returns the credit it spent, and confirming a card
    /// occurrence spends it — and a dropped injection would only show as a stale balance.
    private let onCreditMoved: () -> Void
    private let pollInterval: TimeInterval
    private let now: () -> Date

    private var pollTask: Task<Void, Never>?
    private var eventCancellable: AnyCancellable?
    private var quoteInFlight = false
    /// The server judges the texts in force for the visit's own market, which the consent read cannot see, so
    /// once it refuses a confirm over the terms no later read may hide the tick again.
    private var termsRefused = false

    private static let termsNotAcceptedCode = "consent.terms_not_accepted"

    init(
        orderId: String,
        client: OrderClient,
        repository: OrderRepository,
        membershipRepository: MembershipRepository,
        marketStore: MarketStore,
        snackbar: SnackbarController,
        eventBus: OrderEventBus,
        paymentIntentClient: PaymentIntentClient = LivePaymentIntentClient(),
        consentClient: ConsentStatusClient = LiveConsentStatusClient(),
        liveActivity: OrderLiveActivitySyncing = LiveActivityBridge(),
        onCreditMoved: @escaping () -> Void,
        // Active-order tracking cadence. Short so an OnTheWay → InProgress change surfaces (and the Live
        // Activity is updated) within ~30s while the detail screen is open, instead of up to 5 minutes.
        // Scoped to active orders and only ticks while foregrounded (the sleeping task suspends in the
        // background), so the extra polling is bounded. App-closed immediacy still needs the backend push.
        pollInterval: TimeInterval = 30,
        now: @escaping () -> Date = Date.init
    ) {
        self.orderId = orderId
        self.client = client
        self.repository = repository
        self.membershipRepository = membershipRepository
        self.marketStore = marketStore
        self.snackbar = snackbar
        self.eventBus = eventBus
        self.paymentIntentClient = paymentIntentClient
        self.consentClient = consentClient
        self.liveActivity = liveActivity
        self.onCreditMoved = onCreditMoved
        self.pollInterval = pollInterval
        self.now = now
        super.init()
        membershipRepository.$current
            .map { $0?.hasMembership }
            .assign(to: &$hasMembership)
        marketStore.$state.assign(to: &$markets)
        subscribeToEvents()
    }

    /// Whether the footer offers Cancel: the server's set, read off the loaded order's status and, past
    /// the booked start, off whether a cleaner is on the job.
    var canCancel: Bool {
        OrderStatusGroup.isCancellable(state.loadedValue?.status) && !canReportCleanerNoShow
    }

    /// Whether the footer offers "the cleaner did not arrive" in Cancel's place. Re-read on every render,
    /// and the active-order poller re-renders a staffed order across its start.
    var canReportCleanerNoShow: Bool {
        guard let order = state.loadedValue else { return false }
        return OrderStatusGroup.isAwaitingCleanerPastStart(
            order.status,
            hasCleaner: !order.assignedEmployees.isEmpty,
            startsAt: order.cleaningDateTime,
            now: now()
        )
    }

    /// No card charge for the cancel sheet to promise back (`CustomerOrderDetail.tookNoCardPayment`).
    var tookNoCardPayment: Bool {
        state.loadedValue?.tookNoCardPayment ?? false
    }

    /// Gates the "Make this recurring" shortcut, from the same nullable membership the
    /// recurring list resolves. Nothing on this screen used to fetch that answer, so a
    /// paid-up member lost the shortcut whenever no other screen had warmed the cache.
    var recurringAuthoring: RecurringAuthoringGate {
        .resolve(hasMembership: hasMembership)
    }

    deinit {
        pollTask?.cancel()
    }

    func load() async {
        if orderId.isBlank {
            state = .error(ApiError(code: "missing_order_id"))
            return
        }
        // The in-progress hero plays the heavy 63-frame cleaning mascot. Kick its off-main decode BEFORE
        // the fetch so it lands while the request is in flight: prewarming once the order is loaded runs in
        // the same main-thread turn as the hero's first render, so it can never win that race.
        AnimatedMascotView.prewarm(.cleaningInProgress)
        let initial = state.loadedValue == nil
        // Concurrent, not sequential: the order is what this screen renders, and it must
        // not wait on the answer that decides one footer button or names its market.
        async let membership: Void = refreshMembership()
        async let market: Void = marketStore.refreshIfStale()
        await fetch(initial: initial)
        await loadConsentStatus()
        await membership
        await market
    }

    /// A screen that gates on membership fetches it. Reading whatever another screen
    /// happened to warm is what took the shortcut away from paid-up members on a cold
    /// entry. Failure leaves `hasMembership` nil, which fails open.
    private func refreshMembership() async {
        guard membershipRepository.staleness.isStale else { return }
        await membershipRepository.refresh()
    }

    func retry() async {
        state = .loading
        await fetch(initial: true)
        await loadConsentStatus()
    }

    private func fetch(initial: Bool) async {
        if initial { state = .loading }
        switch await client.getById(orderId: orderId) {
        case let .success(order):
            state = .loaded(order)
            evaluatePoller(for: order)
            syncLiveActivity(for: order)
        case let .failure(error):
            snackbar.showApiError(error)
            if state.loadedValue == nil {
                state = .error(error)
            }
        }
    }

    // MARK: - Active-order poller

    private func evaluatePoller(for order: CustomerOrderDetail) {
        if OrderStatusGroup.isActive(order.status) {
            guard pollTask == nil else { return }
            pollTask = Task { [weak self, pollInterval] in
                while !Task.isCancelled {
                    try? await Task.sleep(nanoseconds: UInt64(pollInterval * 1_000_000_000))
                    guard !Task.isCancelled else { return }
                    await self?.pollTick()
                    if await self?.shouldStopPolling() == true { return }
                }
            }
        } else {
            pollTask?.cancel()
            pollTask = nil
        }
    }

    private func pollTick() async {
        if case let .success(order) = await client.getById(orderId: orderId) {
            state = .loaded(order)
            syncLiveActivity(for: order)
        }
    }

    /// Drive the in-progress-clean Live Activity off the order status (ADR-0029 D2): start (idempotent)
    /// once the cleaner is in the service window — OnTheWay / InProgress — and end it on a terminal status.
    /// The window carries both the booked appointment (mirroring the tracking hero: `cleaningDateTime` +
    /// `estimatedTime` minutes) and the actual phase timestamps off the status history, so the card's ETA
    /// counts against what really happened.
    private func syncLiveActivity(for order: CustomerOrderDetail) {
        guard let orderId = order.id, !orderId.isBlank else { return }
        let status = order.status
        let orderNumber = order.displayOrderNumber ?? ""
        if let wireStatus = OrderStatusGroup.liveActivityStatus(status) {
            guard let window = EtaWindow.forOrder(order) else { return }
            // start is idempotent (creates the activity once, with the current status); update rewrites a
            // running activity so an OnTheWay → InProgress transition flips the card to "Cleaning in progress".
            liveActivity.start(orderId: orderId, orderNumber: orderNumber, status: wireStatus, window: window)
            liveActivity.update(orderId: orderId, orderNumber: orderNumber, status: wireStatus, window: window)
        } else if let terminal = terminalLiveActivityStatus(status) {
            // The terminal status travels with the end: it is what the card is left showing, and what
            // decides whether the card lingers for a last glance or leaves at once.
            liveActivity.end(orderId: orderId, orderNumber: orderNumber, status: terminal)
        }
    }

    private func terminalLiveActivityStatus(_ status: OrderStatus?) -> LiveActivityTerminalStatus? {
        if OrderStatusGroup.isCompleted(status) { return .completed }
        if OrderStatusGroup.isCancelled(status) { return .cancelled }
        return nil
    }

    private func shouldStopPolling() -> Bool {
        guard let order = state.loadedValue else { return true }
        if !OrderStatusGroup.isActive(order.status) {
            pollTask = nil
            return true
        }
        return false
    }

    private func subscribeToEvents() {
        eventCancellable = eventBus.events
            .filter { [orderId] in $0.orderId == orderId }
            .sink { [weak self] _ in
                Task { await self?.refetch() }
            }
    }

    /// A push can land the first copy of a visit awaiting confirmation — after a failed opening, say — and the
    /// confirm stays closed until the consents behind it are read.
    private func refetch() async {
        await fetch(initial: false)
        await loadConsentStatus()
    }

    // MARK: - Cancel

    func cancel(reason: String?) async {
        guard !orderId.isBlank, !cancelState.isSubmitting else { return }
        cancelState = .submitting
        let trimmed = reason?.trimmingCharacters(in: .whitespacesAndNewlines)
        let payload = (trimmed?.isEmpty ?? true) ? nil : trimmed
        switch await client.cancel(orderId: orderId, reason: payload) {
        case let .success(response):
            let currency = state.loadedValue?.currencyCode
            let message: String = if let refunded = response.refunded {
                L10n.OrderCancel.successWithRefund(OrdersFormat.price(refunded, currencyCode: currency))
            } else {
                L10n.OrderCancel.successNoRefund
            }
            snackbar.showSuccess(message)
            cancelState = .idle
            cancelSucceeded.send(response)
            onCreditMoved()
            _ = await repository.refresh()
            await fetch(initial: false)
        case let .failure(error):
            snackbar.showApiError(error)
            cancelState = .error(L10n.OrderCancel.retryHint)
        }
    }

    func dismissCancelError() {
        if case .error = cancelState { cancelState = .idle }
    }

    /// What cancelling costs, asked of the server every time the sheet opens: the tier turns on the
    /// clock, on this customer's own free-cancellation window and on whether a cleaner has taken the
    /// job, so the answer is only good for the moment it was asked. A failure resolves to `.error`
    /// rather than lingering on `.loading` — the sheet degrades to its neutral prompt and the
    /// cancellation stays available, and it is deliberately not snackbarred over a sheet the customer
    /// opened to do something else.
    func loadCancellationQuote() async {
        guard !quoteInFlight else { return }
        guard !orderId.isBlank else {
            cancellationQuote = .error(ApiError(code: "missing_order_id"))
            return
        }
        quoteInFlight = true
        defer { quoteInFlight = false }
        cancellationQuote = .loading
        switch await client.cancellationQuote(orderId: orderId) {
        case let .success(quote):
            cancellationQuote = .loaded(quote)
        case let .failure(error):
            cancellationQuote = .error(error)
        }
    }

    // MARK: - Review

    func submitReview(
        rating: Int,
        comment: String?,
        tags: [CustomerReviewTag] = [],
        isEdit: Bool,
        // Optional per-item scores. `rating` stays the headline and stays required — these add what
        // one number cannot say, which is that the oven was spotless and the bathroom was skipped.
        lines: [OrderItemLineScore] = []
    ) async {
        guard !orderId.isBlank, (1 ... 5).contains(rating), !reviewState.isSubmitting else { return }
        reviewState = .submitting
        let trimmed = comment?.trimmingCharacters(in: .whitespacesAndNewlines).prefix(1000)
        let payload = (trimmed?.isEmpty ?? true) ? nil : String(trimmed ?? "")
        // Filtered to the rating's own polarity here as well as in the sheet. The server refuses a
        // mismatch outright, and a stale selection left behind by a rating change would otherwise
        // turn a valid review into a 400 the customer cannot act on.
        let wantPositive = rating >= CustomerReviewTag.positiveRatingFloor
        let polar = Array(
            tags.filter { $0.isPositive == wantPositive }
                .reduce(into: [CustomerReviewTag]()) { acc, tag in
                    if !acc.contains(tag) { acc.append(tag) }
                }
                .prefix(CustomerReviewTag.maxTags)
        )
        // Only rows actually scored, and only inside 1...5. An unscored item is not a zero — the
        // server refuses a rating outside the range, and "not scored" is a real answer with no line.
        let scored = lines.filter { (1 ... 5).contains($0.rating) }
        switch await client.submitReview(
            orderId: orderId, rating: rating, comment: payload, tags: polar, lines: scored
        ) {
        case let .success(review):
            snackbar.showSuccess(isEdit ? L10n.OrderReview.updated : L10n.OrderReview.success)
            reviewState = .idle
            reviewSucceeded.send(review)
            await fetch(initial: false)
        case let .failure(error):
            snackbar.showApiError(error)
            reviewState = .error(L10n.OrderReview.retryHint)
        }
    }

    func dismissReviewError() {
        if case .error = reviewState { reviewState = .idle }
    }

    // MARK: - Receipt

    func downloadReceipt() async {
        guard !orderId.isBlank, !receiptState.isSubmitting else { return }
        receiptState = .submitting
        switch await client.downloadReceipt(orderId: orderId) {
        case let .success(url):
            receiptState = .idle
            receiptReady.send(url)
        case let .failure(error):
            snackbar.showApiError(error)
            receiptState = .idle
        }
    }

    // MARK: - Confirm recurring

    /// The order is read through the customer's own session, so a card occurrence awaiting confirmation is a
    /// signed-in card payment.
    var offersCardSaving: Bool {
        guard let order = state.loadedValue else { return false }
        return order.needsConfirmation && order.paymentType?.value == 2
    }

    func setSaveCard(_ save: Bool) {
        saveCard = save
    }

    /// Confirming a visit accepts the terms in force, and a visit generated days ahead can postdate the ones the
    /// account last accepted — so the booking review's tick is asked here on the same rule.
    var asksForTerms: Bool {
        alreadyConsented == false
    }

    /// Closed until the consents are read, so the box never flashes up over an account that already holds them.
    var canConfirmRecurring: Bool {
        alreadyConsented == true || (asksForTerms && termsAccepted)
    }

    func setTermsAccepted(_ accepted: Bool) {
        termsAccepted = accepted
    }

    /// Re-read at every opening, as the booking sheet does: the answer belongs to the account and can change
    /// under a live session.
    private func loadConsentStatus() async {
        guard state.loadedValue?.needsConfirmation == true, !termsRefused else { return }
        let held = await consentClient.holdsTermsTickConsents()
        alreadyConsented = held && !termsRefused
    }

    /// A recurring-generated order the server marks `needsConfirmation` needs an
    /// explicit confirm. The backend branches on payment type: a cash response carries
    /// no `clientSecret` (confirmed, still unpaid until the cleaner takes the cash)
    /// → success + refetch; a card response carries a `clientSecret` → the sheet's
    /// intent is asked of CreatePaymentIntent with the save tick. `.completed` is
    /// UX-only — the view calls `notifyRecurringPaymentResult` and we re-read the
    /// order; the webhook remains the sole paid authority.
    func confirmRecurring() async {
        guard !orderId.isBlank, !confirmRecurringState.isSubmitting else { return }
        confirmRecurringState = .submitting
        // Asserted only when the box was shown and ticked; an account that saw no box asserts nothing new.
        let terms: Bool? = asksForTerms && termsAccepted ? true : nil
        switch await client.confirmRecurring(orderId: orderId, termsAccepted: terms) {
        case let .success(confirmation):
            if confirmation.needsPayment {
                // The server takes the occurrence's credit before it mints the intent, so the balance has
                // moved whether or not the sheet is then completed.
                onCreditMoved()
                await presentCardPayment()
            } else {
                confirmRecurringState = .idle
                snackbar.showSuccess(L10n.Recurring.confirmSuccess)
                _ = await repository.refresh()
                await fetch(initial: false)
            }
        case let .failure(error) where error.refusesForUnpaidAmount:
            owesMoney = true
            confirmRecurringState = .idle
        case let .failure(error):
            snackbar.showApiError(error)
            if error.code == Self.termsNotAcceptedCode {
                termsRefused = true
                alreadyConsented = false
                termsAccepted = false
            }
            confirmRecurringState = .idle
        }
    }

    func dismissOwesMoney() {
        owesMoney = false
    }

    /// ConfirmRecurringOrder takes no saveCard and its intent keeps nothing. CreatePaymentIntent hands that
    /// intent back unticked and replaces it with a card-saving one ticked. It is asked on every card confirm:
    /// a confirm repeated after a ticked attempt replays the intent that attempt cancelled.
    private func presentCardPayment() async {
        let savesCard = saveCard
        let result = await paymentIntentClient.createPaymentIntent(orderId: orderId, saveCard: savesCard)
        confirmRecurringState = .idle
        switch result {
        case let .success(intent):
            recurringCardPayment.send(.cardPayment(
                clientSecret: intent.clientSecret,
                ephemeralKey: intent.ephemeralKey,
                stripeCustomerId: intent.stripeCustomerId,
                intentSavesCard: savesCard
            ))
        case let .failure(error):
            snackbar.showApiError(error)
        }
    }

    func notifyRecurringPaymentResult(_ outcome: PaymentSheetOutcome) async {
        switch outcome {
        case .completed:
            snackbar.showSuccess(L10n.Recurring.confirmSuccess)
            _ = await repository.refresh()
            await fetch(initial: false)
        case .canceled:
            break
        case .failed:
            snackbar.showError(L10n.localized("error_payment_failed"))
        }
    }

    // MARK: - Photos side-channel

    /// Fetch photos lazily once the detail is loaded (or retry after an error).
    /// SAS URLs carry a ~1h TTL, so each detail open fetches fresh — no cache
    /// across re-opens (`OrderDetailViewModel.kt:498-506`).
    func ensurePhotosLoaded() async {
        guard !orderId.isBlank else { return }
        switch photos {
        case .idle, .error:
            break
        case .loading, .loaded:
            return
        }
        photos = .loading
        switch await client.getPhotos(orderId: orderId) {
        case let .success(response):
            photos = .loaded(response)
        case .failure:
            photos = .error
        }
    }
}
