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
    static let cardReads = SavedCardCaptureStore.cardReads

    @Published private(set) var state: UiState<PaymentsSnapshot> = .loading
    @Published private(set) var removeState: ActionState = .idle
    @Published private(set) var payState: ActionState = .idle
    @Published private(set) var cardConsentAccepted = false
    @Published private(set) var cardCapture: SavedCardCapture?

    var addCardState: ActionState {
        cardCapture?.stage.isBusy == true ? .submitting : .idle
    }

    /// The Stripe Checkout page to open; the screen hands it to the browser.
    let payLinks = PassthroughSubject<URL, Never>()
    /// PaymentSheet in setup mode; the screen presents it and hands the outcome to `cardSheetFinished`.
    let cardSetups = PassthroughSubject<SavedCardSheetRequest, Never>()

    private let savedCardClient: SavedCardClient
    private let receivableClient: ReceivableClient
    private let snackbar: SnackbarController
    private let countryId: String?
    private let currencyCode: String?
    private let isCardPaymentAvailable: Bool
    private let pauseBetweenCardReads: () async -> Void
    private let pendingCards: SavedCardCaptureStore
    private let scope: SavedCardCaptureScope
    private let sessionGeneration: UInt
    private var cancellables: Set<AnyCancellable> = []

    /// A pay link went to the browser, so the list is re-read when the customer comes back.
    private var awaitingPayment = false

    init(
        savedCardClient: SavedCardClient,
        receivableClient: ReceivableClient,
        snackbar: SnackbarController,
        countryId: String? = nil,
        currencyCode: String? = nil,
        isCardPaymentAvailable: Bool = StripeConfig.isCardPaymentAvailable,
        pendingCards: SavedCardCaptureStore,
        pauseBetweenCardReads: @escaping () async -> Void = { _ = try? await Task.sleep(nanoseconds: 1_500_000_000) }
    ) {
        self.savedCardClient = savedCardClient
        self.receivableClient = receivableClient
        self.snackbar = snackbar
        self.countryId = countryId
        self.currencyCode = currencyCode
        self.isCardPaymentAvailable = isCardPaymentAvailable
        self.pauseBetweenCardReads = pauseBetweenCardReads
        self.pendingCards = pendingCards
        scope = SavedCardCaptureScope(countryId: countryId, currencyCode: currencyCode)
        sessionGeneration = pendingCards.generation
        pendingCards.activate(scope)
        cardCapture = pendingCards.capture(for: scope)
        super.init()
        pendingCards.changes.sink { [weak self] in self?.captureChanged() }.store(in: &cancellables)
        pendingCards.events.sink { [weak self] event in self?.receive(event) }.store(in: &cancellables)
    }

    /// Offered while the customer holds no usable card in the market's currency, in a build that can save one.
    /// With no market known, any usable card counts.
    var offersCardCapture: Bool {
        guard isCurrentSession, pendingCards.activeScope == scope, cardCapture == nil,
              isCardPaymentAvailable, case let .loaded(snapshot) = state else { return false }
        guard let currencyCode else { return !snapshot.cards.contains { $0.isUsable(on: Date()) } }
        return SavedCard.usable(in: snapshot.cards, currencyCode: currencyCode) == nil
    }

    private var isCurrentSession: Bool {
        sessionGeneration == pendingCards.generation
    }

    func load() async {
        guard isCurrentSession, pendingCards.activeScope == scope else { return }
        if state.loadedValue == nil { state = .loading }
        let result = await read()
        guard isCurrentSession, pendingCards.activeScope == scope else { return }
        switch result {
        case let .success(snapshot):
            state = .loaded(snapshot)
            if !pendingCards.reconcile(snapshot.cards, in: scope) {
                await checkSavedCard()
            }
        case let .failure(error):
            if state.loadedValue == nil { state = .error(error) }
            snackbar.showApiError(error)
        }
    }

    func onResumed() async {
        guard isCurrentSession, pendingCards.activeScope == scope else { return }
        if awaitingPayment {
            awaitingPayment = false
            await load()
        } else {
            await checkSavedCard()
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
        guard offersCardCapture, cardConsentAccepted,
              let attempt = pendingCards.begin(in: scope) else { return }
        let result = await savedCardClient.startCapture(consentAccepted: true, countryId: countryId)
        guard isCurrentSession, pendingCards.isCurrent(attempt, in: scope) else { return }
        switch result {
        case let .success(setup):
            guard pendingCards.install(setup, attempt: attempt, in: scope) else { return }
            continueCardSetup()
        case let .failure(error):
            pendingCards.discard(in: scope, attempt: attempt)
            if pendingCards.activeScope == scope { snackbar.showApiError(error) }
        }
    }

    func continueCardSetup() {
        guard isCurrentSession, pendingCards.activeScope == scope,
              let capture = cardCapture, case let .awaitingSheet(setup) = capture.stage else { return }
        cardSetups.send(SavedCardSheetRequest(
            attempt: capture.attempt,
            presentation: PaymentSheetPresentation(
                clientSecret: setup.setupIntentClientSecret, ephemeralKey: setup.ephemeralKey,
                stripeCustomerId: setup.stripeCustomerId, merchantDisplayName: "Cleansia", intentKind: .setup
            )
        ))
    }

    func claimCardSheet(_ attempt: SavedCardCaptureAttempt) -> Bool {
        guard isCurrentSession else { return false }
        return pendingCards.takeSheet(attempt: attempt, in: scope) != nil
    }

    func cardSheetFinished(_ outcome: PaymentSheetOutcome, attempt: SavedCardCaptureAttempt) async {
        guard isCurrentSession, pendingCards.isCurrent(attempt, in: scope) else { return }
        switch outcome {
        case .completed:
            guard pendingCards.markPending(attempt: attempt, in: scope) else { return }
            await checkSavedCard()
        case .canceled, .failed:
            pendingCards.discard(in: scope, attempt: attempt)
            if pendingCards.activeScope == scope { snackbar.showError(L10n.Payments.cardAddCancelled) }
        }
    }

    func checkSavedCard() async {
        guard isCurrentSession, pendingCards.activeScope == scope else { return }
        await pendingCards.verify(in: scope, client: savedCardClient, pause: pauseBetweenCardReads)
    }

    func restartCardSetup() {
        guard isCurrentSession, pendingCards.activeScope == scope else { return }
        pendingCards.discard(in: scope)
        cardConsentAccepted = false
    }

    private func captureChanged() {
        guard isCurrentSession else {
            cardCapture = nil
            cardConsentAccepted = false
            state = .loading
            return
        }
        cardCapture = pendingCards.capture(for: scope)
    }

    private func receive(_ event: SavedCardCaptureStore.Event) {
        guard isCurrentSession, pendingCards.activeScope == scope else { return }
        switch event {
        case let .observed(eventScope, cards) where eventScope == scope:
            updateCards(cards)
        case let .confirmed(eventScope, cards) where eventScope == scope:
            updateCards(cards)
            cardConsentAccepted = false
            snackbar.showSuccess(L10n.Payments.cardAdded)
        case let .failure(eventScope, error) where eventScope == scope:
            snackbar.showApiError(error)
        default:
            break
        }
    }

    private func updateCards(_ cards: [SavedCard]) {
        if let snapshot = state.loadedValue {
            state = .loaded(PaymentsSnapshot(receivables: snapshot.receivables, cards: cards))
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
