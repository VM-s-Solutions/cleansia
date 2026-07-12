import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// Per-action discriminator so individual lifecycle buttons can show their own
/// spinner (the `OrderAction` parity).
enum OrderAction: Equatable {
    case take
    case notifyOnTheWay
    case start
    case complete

    var mutation: OrdersMutation {
        switch self {
        case .take: .takeOrder
        case .notifyOnTheWay: .notifyOnTheWay
        case .start: .startOrder
        case .complete: .completeOrder
        }
    }

    /// Take stays silent — the action visibly flips. The slide transitions
    /// confirm out loud: their only passive cue is a quiet label swap, which
    /// cleaners read as a failed swipe. One mapping for the list AND the detail.
    var successFeedback: String? {
        switch self {
        case .notifyOnTheWay: L10n.Orders.customerNotifiedOnTheWay
        case .start: L10n.Orders.orderStartedToast
        case .complete: L10n.Orders.orderCompletedToast
        case .take: nil
        }
    }
}

@MainActor
final class OrderDetailViewModel: ViewModel {
    @Published private(set) var state: UiState<OrderDetail> = .loading
    @Published private(set) var actionState: ActionState = .idle
    @Published private(set) var inFlightAction: OrderAction?

    private let orderId: String
    private let client: PartnerOrderClient
    private let staleness: OrdersStaleness
    private let snackbar: SnackbarController

    init(
        orderId: String,
        client: PartnerOrderClient,
        staleness: OrdersStaleness,
        snackbar: SnackbarController
    ) {
        self.orderId = orderId
        self.client = client
        self.staleness = staleness
        self.snackbar = snackbar
    }

    /// The one valid primary action for the loaded order (the shared machine).
    var primaryAction: OrderPrimaryAction {
        guard let order = state.loadedValue else { return .none }
        return OrderPrimaryAction.action(
            for: order.status,
            isMine: order.isAssignedToCurrentUser,
            hasAfterPhotos: order.hasAfterPhotos
        )
    }

    func load() async {
        await fetch()
    }

    func dispatch(_ action: OrderPrimaryAction) async {
        switch action {
        case .take: await take()
        case .notifyOnTheWay: await notifyOnTheWay()
        case .start: await start()
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
            snackbar.showApiError(error)
            actionState = .error(ApiErrorLocalizer().message(for: error))
            staleness.invalidateOrder(orderId)
            await fetch()
        }
    }
}
