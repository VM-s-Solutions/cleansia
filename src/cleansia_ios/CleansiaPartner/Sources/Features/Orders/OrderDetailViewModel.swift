import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

/// Per-action discriminator so individual lifecycle buttons can show their own
/// spinner (the `OrderAction` parity).
enum OrderAction: Equatable {
    case take
    case notifyOnTheWay
    case start
    case markCashCollected
    case complete
    case declineOffer

    /// Only the two reservation actions earn platform-owned framing; every other refusal on the detail
    /// reaches the snackbar exactly as it always did.
    var refusalKind: OfferRefusal.Kind? {
        switch self {
        case .take: .confirm
        case .declineOffer: .release
        case .notifyOnTheWay, .start, .markCashCollected, .complete: nil
        }
    }

    var mutation: OrdersMutation {
        switch self {
        case .take: .takeOrder
        case .notifyOnTheWay: .notifyOnTheWay
        case .start: .startOrder
        case .markCashCollected: .markCashCollected
        case .complete: .completeOrder
        case .declineOffer: .declinePreferredOffer
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
        case .complete: L10n.Orders.orderCompletedToast
        case .declineOffer: L10n.Offers.declinedToast
        case .take: nil
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

    private let orderId: String
    private let client: PartnerOrderClient
    private let staleness: OrdersStaleness
    private let snackbar: SnackbarController
    private let pendingOffers: PendingOffersStore

    init(
        orderId: String,
        client: PartnerOrderClient,
        staleness: OrdersStaleness,
        snackbar: SnackbarController,
        pendingOffers: PendingOffersStore
    ) {
        self.orderId = orderId
        self.client = client
        self.staleness = staleness
        self.snackbar = snackbar
        self.pendingOffers = pendingOffers
        super.init()
        pendingOffers.$offers
            .map { offers in offers.first { $0.id == orderId } }
            .assign(to: &$preferredOffer)
    }

    /// Re-read on every state change, never decided once at screen-open: the cleaner can take the job
    /// with the detail already in front of them, and the rails appear the moment they do.
    var canReadPhotos: Bool {
        state.loadedValue?.showsWorkSections == true
    }

    /// The one valid primary action for the loaded order (the shared machine).
    var primaryAction: OrderPrimaryAction {
        guard let order = state.loadedValue else { return .none }
        return OrderPrimaryAction.action(
            for: order.status,
            isMine: order.isAssignedToCurrentUser,
            hasAfterPhotos: order.hasAfterPhotos,
            isCashPayment: order.payment.isCash,
            isPaymentSettled: order.payment.isSettled
        )
    }

    func load() async {
        // The sheet-edge puck plays the heavy 125-frame cleaning loop once the order is in progress.
        // Kick its off-main decode BEFORE the fetch so it lands while the request is in flight —
        // prewarming after the order loads shares a main-thread turn with the puck's first render.
        AnimatedMascotView.prewarm(.cleaningInProgress)
        await ensureOffersFresh()
        await fetch()
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

    func dispatch(_ action: OrderPrimaryAction) async {
        switch action {
        case .take: await take()
        case .notifyOnTheWay: await notifyOnTheWay()
        case .start: await start()
        case .collectCash: await markCashCollected()
        case .complete: await complete()
        case .completeBlocked, .none: break
        }
    }

    func take() async {
        await run(.take) { await self.client.takeOrder(orderId: self.orderId) }
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

    func complete() async {
        await run(.complete) {
            await self.client.completeOrder(orderId: self.orderId, actualMinutes: nil, notes: nil)
        }
    }

    private func fetch() async {
        // O2: always acts on the orderId this VM was constructed with (the id
        // the list/detail response carried) — never a synthesized/echoed id.
        switch await client.getById(orderId: orderId) {
        case let .success(item):
            state = .loaded(OrderDetail(item))
        case let .failure(error):
            snackbar.showApiError(error)
            // Stay .loaded through a background-refetch failure — only the
            // first load (nothing loaded yet) flips to .error
            // (OrderDetailViewModel.kt:74-79 parity).
            if state.loadedValue == nil {
                state = .error(error)
            }
        }
    }

    private func run(_ action: OrderAction, _ block: @escaping () async -> ApiResult<Void>) async {
        // Re-entry guard: one mutation in flight (O4).
        guard !actionState.isSubmitting else { return }
        actionState = .submitting
        inFlightAction = action

        switch await block() {
        case .success:
            if let confirmation = action.successFeedback {
                snackbar.showSuccess(confirmation)
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
