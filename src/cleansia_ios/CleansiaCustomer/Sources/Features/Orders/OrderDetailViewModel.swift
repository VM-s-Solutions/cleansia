import CleansiaCore
import CleansiaCustomerApi
import Combine
import Foundation

enum PhotosUiState {
    case idle
    case loading
    case loaded(GetOrderPhotosResponse)
    case error
}

extension PhotosUiState {
    var loadedResponse: GetOrderPhotosResponse? {
        if case let .loaded(response) = self { return response }
        return nil
    }
}

@MainActor
final class OrderDetailViewModel: ViewModel {
    @Published private(set) var state: UiState<OrderItem> = .loading
    @Published private(set) var photos: PhotosUiState = .idle
    @Published private(set) var cancelState: ActionState = .idle
    @Published private(set) var reviewState: ActionState = .idle
    @Published private(set) var receiptState: ActionState = .idle
    @Published private(set) var confirmRecurringState: ActionState = .idle

    let cancelSucceeded = PassthroughSubject<CancelOrderResponse, Never>()
    let reviewSucceeded = PassthroughSubject<OrderReviewDto, Never>()
    let receiptReady = PassthroughSubject<URL, Never>()
    let recurringCardPayment = PassthroughSubject<PaymentSheetPresentation, Never>()

    private let orderId: String
    private let client: OrderClient
    private let repository: OrderRepository
    private let snackbar: SnackbarController
    private let eventBus: OrderEventBus
    private let liveActivity: OrderLiveActivitySyncing
    private let pollInterval: TimeInterval
    private let now: () -> Date

    private var pollTask: Task<Void, Never>?
    private var eventCancellable: AnyCancellable?

    init(
        orderId: String,
        client: OrderClient,
        repository: OrderRepository,
        snackbar: SnackbarController,
        eventBus: OrderEventBus,
        liveActivity: OrderLiveActivitySyncing = LiveActivityBridge(),
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
        self.snackbar = snackbar
        self.eventBus = eventBus
        self.liveActivity = liveActivity
        self.pollInterval = pollInterval
        self.now = now
        super.init()
        subscribeToEvents()
    }

    deinit {
        pollTask?.cancel()
    }

    func load() async {
        if orderId.isBlank {
            state = .error(ApiError(code: "missing_order_id"))
            return
        }
        await fetch(initial: state.loadedValue == nil)
    }

    func retry() async {
        state = .loading
        await fetch(initial: true)
    }

    private func fetch(initial: Bool) async {
        if initial { state = .loading }
        switch await client.getById(orderId: orderId) {
        case let .success(order):
            state = .loaded(order)
            // The in-progress hero plays the heavy 125-frame cleaning mascot; decode + pin it off-main as
            // the detail loads so it's warm when the hero renders, instead of a ~5s first-paint freeze.
            if order.status == ._4 {
                AnimatedMascotView.prewarm(.cleaningInProgress)
            }
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

    private func evaluatePoller(for order: OrderItem) {
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

    /// Drive the in-progress-clean Live Activity off the order status (ADR-0029 LA-5): start (idempotent)
    /// once the order is active — Confirmed / OnTheWay / InProgress — and end it on a terminal status. The
    /// appointment window mirrors the tracking hero: `cleaningDateTime` + `estimatedTime` minutes.
    private func syncLiveActivity(for order: OrderItem) {
        guard let orderId = order.id, !orderId.isBlank else { return }
        let status = order.status
        if OrderStatusGroup.isActive(status) {
            guard let start = order.cleaningDateTime else { return }
            let end = start.addingTimeInterval(TimeInterval(max(order.estimatedTime ?? 0, 1) * 60))
            let wireStatus = OrderStatusGroup.liveActivityStatus(status)
            let orderNumber = order.displayOrderNumber ?? ""
            // start is idempotent (creates the activity once, with the current status); update rewrites a
            // running activity so an OnTheWay → InProgress transition flips the card to "Cleaning in progress".
            liveActivity.start(
                orderId: orderId, orderNumber: orderNumber, status: wireStatus,
                scheduledStart: start, scheduledEnd: end
            )
            liveActivity.update(
                orderId: orderId, orderNumber: orderNumber, status: wireStatus,
                scheduledStart: start, scheduledEnd: end
            )
        } else if OrderStatusGroup.isCompleted(status) || OrderStatusGroup.isCancelled(status) {
            liveActivity.end(orderId: orderId)
        }
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
                Task { await self?.fetch(initial: false) }
            }
    }

    // MARK: - Cancel

    func cancel(reason: String?) async {
        guard !orderId.isBlank, !cancelState.isSubmitting else { return }
        cancelState = .submitting
        let trimmed = reason?.trimmingCharacters(in: .whitespacesAndNewlines)
        let payload = (trimmed?.isEmpty ?? true) ? nil : trimmed
        switch await client.cancel(orderId: orderId, reason: payload) {
        case let .success(response):
            let currency = state.loadedValue?.currency?.code
            let message: String = if response.refundInitiated == true, (response.refundAmount ?? 0) > 0 {
                L10n.OrderCancel.successWithRefund(OrdersFormat.price(
                    response.refundAmount ?? 0,
                    currencyCode: currency
                ))
            } else {
                L10n.OrderCancel.successNoRefund
            }
            snackbar.showSuccess(message)
            cancelState = .idle
            cancelSucceeded.send(response)
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

    // MARK: - Review

    func submitReview(rating: Int, comment: String?, isEdit: Bool) async {
        guard !orderId.isBlank, (1 ... 5).contains(rating), !reviewState.isSubmitting else { return }
        reviewState = .submitting
        let trimmed = comment?.trimmingCharacters(in: .whitespacesAndNewlines).prefix(2000)
        let payload = (trimmed?.isEmpty ?? true) ? nil : String(trimmed ?? "")
        switch await client.submitReview(orderId: orderId, rating: rating, comment: payload) {
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

    /// A recurring-generated order in payment-pending state needs an explicit
    /// confirm. The backend branches on payment type: a cash response carries no
    /// `clientSecret` (already Confirmed + Paid) → success + refetch; a card
    /// response carries a `clientSecret` → emit a PaymentSheet presentation for
    /// the view to present (PaymentIntent variant). `.completed` is UX-only — the
    /// view calls `notifyRecurringPaymentResult` and we re-read the order; the
    /// webhook remains the sole paid authority.
    func confirmRecurring() async {
        guard !orderId.isBlank, !confirmRecurringState.isSubmitting else { return }
        confirmRecurringState = .submitting
        switch await client.confirmRecurring(orderId: orderId) {
        case let .success(confirmation):
            confirmRecurringState = .idle
            if confirmation.needsPayment,
               let clientSecret = confirmation.clientSecret,
               let stripeCustomerId = confirmation.stripeCustomerId,
               let ephemeralKey = confirmation.ephemeralKey,
               !stripeCustomerId.isEmpty, !ephemeralKey.isEmpty
            {
                recurringCardPayment.send(PaymentSheetPresentation(
                    clientSecret: clientSecret,
                    ephemeralKey: ephemeralKey,
                    stripeCustomerId: stripeCustomerId,
                    merchantDisplayName: "Cleansia",
                    intentKind: .payment
                ))
            } else {
                snackbar.showSuccess(L10n.Recurring.confirmSuccess)
                _ = await repository.refresh()
                await fetch(initial: false)
            }
        case let .failure(error):
            snackbar.showApiError(error)
            confirmRecurringState = .idle
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
