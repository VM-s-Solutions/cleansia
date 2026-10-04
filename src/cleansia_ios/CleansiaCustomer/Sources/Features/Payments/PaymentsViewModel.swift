import CleansiaCore
import Combine
import Foundation

struct PaymentsSnapshot: Equatable {
    let receivables: [Receivable]
    let cards: [SavedCard]
}

/// What the customer owes and the cards they saved.
@MainActor
final class PaymentsViewModel: ViewModel {
    /// How long the screen waits for a just-saved card: 8 reads, `pauseBetweenCardReads` apart.
    static let cardReads = 8

    @Published private(set) var state: UiState<PaymentsSnapshot> = .loading
    @Published private(set) var removeState: ActionState = .idle
    @Published private(set) var payState: ActionState = .idle
    @Published private(set) var cardConsentAccepted = false
    @Published private(set) var addCardState: ActionState = .idle

    /// The Stripe Checkout page to open; the screen hands it to the browser.
    let payLinks = PassthroughSubject<URL, Never>()
    /// PaymentSheet in setup mode; the screen presents it and hands the outcome to `cardSheetFinished`.
    let cardSetups = PassthroughSubject<PaymentSheetPresentation, Never>()

    private let savedCardClient: SavedCardClient
    private let receivableClient: ReceivableClient
    private let snackbar: SnackbarController
    private let countryId: String?
    private let currencyCode: String?
    private let pauseBetweenCardReads: () async -> Void
    private var capturingCardId: String?

    /// A pay link went to the browser, so the list is re-read when the customer comes back.
    private var awaitingPayment = false

    init(
        savedCardClient: SavedCardClient,
        receivableClient: ReceivableClient,
        snackbar: SnackbarController,
        countryId: String? = nil,
        currencyCode: String? = nil,
        pauseBetweenCardReads: @escaping () async -> Void = { _ = try? await Task.sleep(nanoseconds: 1_500_000_000) }
    ) {
        self.savedCardClient = savedCardClient
        self.receivableClient = receivableClient
        self.snackbar = snackbar
        self.countryId = countryId
        self.currencyCode = currencyCode
        self.pauseBetweenCardReads = pauseBetweenCardReads
    }

    /// Offered while the customer holds no usable card in the market's currency. With no market known,
    /// any usable card counts.
    var offersCardCapture: Bool {
        guard case let .loaded(snapshot) = state else { return false }
        guard let currencyCode else { return !snapshot.cards.contains { $0.isUsable(on: Date()) } }
        return SavedCard.usable(in: snapshot.cards, currencyCode: currencyCode) == nil
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
            snackbar.showSuccess(L10n.Payments.cardRemoved)
            if case let .loaded(snapshot) = state {
                state = .loaded(PaymentsSnapshot(
                    receivables: snapshot.receivables,
                    cards: snapshot.cards.filter { $0.id != card.id }
                ))
            }
        case let .failure(error):
            // The confirm closed on the tap, so the snackbar is where a refusal is said; the card stays.
            snackbar.showApiError(error)
            removeState = .idle
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

    func setCardConsentAccepted(_ accepted: Bool) {
        cardConsentAccepted = accepted
    }

    func addCard() async {
        guard cardConsentAccepted, !addCardState.isSubmitting else { return }
        addCardState = .submitting
        switch await savedCardClient.startCapture(consentAccepted: cardConsentAccepted, countryId: countryId) {
        case let .success(setup):
            capturingCardId = setup.savedCardId
            cardSetups.send(PaymentSheetPresentation(
                clientSecret: setup.setupIntentClientSecret,
                ephemeralKey: setup.ephemeralKey,
                stripeCustomerId: setup.stripeCustomerId,
                merchantDisplayName: "Cleansia",
                intentKind: .setup
            ))
        case let .failure(error):
            snackbar.showApiError(error)
            addCardState = .idle
        }
    }

    /// The card reaches the account through Stripe's webhook, so it is read until the server lists it.
    func cardSheetFinished(_ outcome: PaymentSheetOutcome) async {
        guard let cardId = capturingCardId else { return }
        capturingCardId = nil
        switch outcome {
        case .completed:
            if let cards = await awaitCard(id: cardId) {
                cardConsentAccepted = false
                if case let .loaded(snapshot) = state {
                    state = .loaded(PaymentsSnapshot(receivables: snapshot.receivables, cards: cards))
                }
                snackbar.showSuccess(L10n.Payments.cardAdded)
            } else {
                snackbar.showInfo(L10n.Payments.cardAddPending)
            }
        case .canceled, .failed:
            snackbar.showError(L10n.Payments.cardAddCancelled)
        }
        addCardState = .idle
    }

    private func awaitCard(id: String) async -> [SavedCard]? {
        for read in 0 ..< Self.cardReads {
            if read > 0 {
                await pauseBetweenCardReads()
            }
            if case let .success(cards) = await savedCardClient.myCards(), cards.contains(where: { $0.id == id }) {
                return cards
            }
        }
        return nil
    }

    private func read() async -> ApiResult<PaymentsSnapshot> {
        let owed = await receivableClient.myReceivables()
        let held = await savedCardClient.myCards()
        return owed.flatMap { receivables in
            held.map { PaymentsSnapshot(receivables: receivables, cards: $0) }
        }
    }
}
