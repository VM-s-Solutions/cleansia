import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

enum OfferAction: Equatable {
    case confirm
    case decline
}

/// The offer an action is running against, kept beside the refusal it may produce so the screen can
/// name the order it could not hand over.
struct OfferAttempt: Equatable {
    let orderId: String
    let displayOrderNumber: String?
    let action: OfferAction
}

/// Jobs a customer asked for this cleaner by name, held for them until a deadline the server owns.
///
/// Confirming is `takeOrder` — the platform has no confirm command, and a second acquisition path
/// would either duplicate TakeOrder's one ordered gate or be weaker than it. Because nothing gates the
/// reservation on the weekly cap, that gate can refuse a job the cleaner was told was theirs; the
/// refusal is kept as an `ActionState.error` beside the `attempt` it belongs to so the screen can say
/// whose fault it is, rather than dropped on a snackbar as a bare reason.
@MainActor
final class PendingOffersViewModel: ViewModel {
    @Published private(set) var state: UiState<[PendingOfferItem]> = .loading
    @Published private(set) var actionState: ActionState = .idle
    @Published private(set) var attempt: OfferAttempt?

    let confirmed = PassthroughSubject<String, Never>()

    private enum LoadPhase {
        case loading
        case ready
        case failed(ApiError)
    }

    private var phase: LoadPhase = .loading
    private var cancellables: Set<AnyCancellable> = []

    private let store: PendingOffersStore
    private let client: PartnerOrderClient
    private let staleness: OrdersStaleness
    private let snackbar: SnackbarController

    init(
        store: PendingOffersStore,
        client: PartnerOrderClient,
        staleness: OrdersStaleness,
        snackbar: SnackbarController
    ) {
        self.store = store
        self.client = client
        self.staleness = staleness
        self.snackbar = snackbar
        super.init()
        store.$offers
            .sink { [weak self] offers in self?.resolveState(offers) }
            .store(in: &cancellables)
    }

    func load() async {
        guard store.isStale else {
            phase = .ready
            resolveState(store.offers)
            return
        }
        await fetch()
    }

    func confirm(_ offer: PendingOfferItem) async {
        await run(offer, .confirm)
    }

    func decline(_ offer: PendingOfferItem) async {
        await run(offer, .decline)
    }

    func dismissRefusal() {
        actionState = .idle
        attempt = nil
    }

    /// A warm cache still wins over the phase: a list that has rows is a loaded list whatever the last
    /// fetch did, and an empty answer the server gave is a loaded empty list rather than a failure.
    private func resolveState(_ offers: [PendingOfferItem]) {
        guard offers.isEmpty else {
            state = .loaded(offers)
            return
        }
        switch phase {
        case .loading: state = .loading
        case let .failed(error): state = .error(error)
        case .ready: state = .loaded([])
        }
    }

    private func fetch() async {
        switch await store.refresh() {
        case .success: phase = .ready
        case let .failure(error): phase = .failed(error)
        }
        resolveState(store.offers)
    }

    private func run(_ offer: PendingOfferItem, _ action: OfferAction) async {
        guard let orderId = offer.id, !orderId.isEmpty else { return }
        guard !actionState.isSubmitting else { return }
        attempt = OfferAttempt(orderId: orderId, displayOrderNumber: offer.displayOrderNumber, action: action)
        actionState = .submitting

        let result: ApiResult<Void> = switch action {
        case .confirm: await client.takeOrder(orderId: orderId)
        case .decline: await store.decline(orderId: orderId)
        }

        switch result {
        case .success:
            actionState = .idle
            attempt = nil
            switch action {
            case .confirm:
                staleness.invalidatePanes(for: .takeOrder)
                staleness.invalidateOrder(orderId)
                confirmed.send(orderId)
            case .decline:
                snackbar.showSuccess(L10n.Offers.declinedToast)
            }
        case let .failure(error):
            let reason = ApiErrorLocalizer().message(for: error)
            switch action {
            // The screen frames this one; a snackbar carrying the same bare reason would land on top
            // of the sentence that says the platform, not the cleaner, is at fault.
            case .confirm:
                actionState = .error(reason)
            case .decline:
                actionState = .idle
                attempt = nil
                snackbar.showError(reason)
            }
        }

        // The server decides whether the offer survived either outcome — a confirm the cap refused
        // leaves the reservation live, a taken seat removes it.
        await fetch()
    }
}
