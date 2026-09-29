import CleansiaCore
import Combine
import Foundation

struct PaymentsSnapshot: Equatable {
    let receivables: [Receivable]
    let cards: [SavedCard]
}

/// What the customer owes and the card that guarantees their cash bookings.
@MainActor
final class PaymentsViewModel: ViewModel {
    @Published private(set) var state: UiState<PaymentsSnapshot> = .loading
    @Published private(set) var removeState: ActionState = .idle
    @Published private(set) var payState: ActionState = .idle

    let removed = PassthroughSubject<String, Never>()
    /// The Stripe Checkout page to open; the screen hands it to the browser.
    let payLinks = PassthroughSubject<URL, Never>()

    private let savedCardClient: SavedCardClient
    private let receivableClient: ReceivableClient
    private let snackbar: SnackbarController

    /// A pay link went to the browser, so the list is re-read when the customer comes back.
    private var awaitingPayment = false

    init(savedCardClient: SavedCardClient, receivableClient: ReceivableClient, snackbar: SnackbarController) {
        self.savedCardClient = savedCardClient
        self.receivableClient = receivableClient
        self.snackbar = snackbar
    }

    func load() async {
        state = .loading
        switch await read() {
        case let .success(snapshot):
            state = .loaded(snapshot)
        case let .failure(error):
            state = .error(error)
            snackbar.showApiError(error)
        }
    }

    func onResumed() async {
        guard awaitingPayment else { return }
        awaitingPayment = false
        if case let .success(snapshot) = await read() {
            state = .loaded(snapshot)
        }
    }

    func remove(_ card: SavedCard) async {
        guard !removeState.isSubmitting else { return }
        removeState = .submitting
        switch await savedCardClient.remove(savedCardId: card.id) {
        case .success:
            removeState = .idle
            removed.send(card.id)
            snackbar.showSuccess(L10n.Payments.cardRemoved)
            if case let .loaded(snapshot) = state {
                state = .loaded(PaymentsSnapshot(
                    receivables: snapshot.receivables,
                    cards: snapshot.cards.filter { $0.id != card.id }
                ))
            }
        case let .failure(error):
            snackbar.showApiError(error)
            removeState = .error(L10n.Payments.cardRemoveRetryHint)
        }
    }

    func pay(_ receivable: Receivable) async {
        guard !payState.isSubmitting else { return }
        payState = .submitting
        switch await receivableClient.payLink(receivableId: receivable.id) {
        case let .success(url):
            payState = .idle
            awaitingPayment = true
            payLinks.send(url)
        case let .failure(error):
            snackbar.showApiError(error)
            payState = .idle
        }
    }

    private func read() async -> ApiResult<PaymentsSnapshot> {
        let owed = await receivableClient.myReceivables()
        let held = await savedCardClient.myCards()
        return owed.flatMap { receivables in
            held.map { PaymentsSnapshot(receivables: receivables, cards: $0) }
        }
    }
}
