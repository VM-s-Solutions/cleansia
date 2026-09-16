import CleansiaCore
import Foundation

enum GuestOrderUiState: Equatable {
    case empty
    case loading
    case error(String)
    case loaded(GuestOrder)
    case cancelled(String)
}

extension GuestOrderUiState {
    var loadedOrder: GuestOrder? {
        if case let .loaded(order) = self { return order }
        return nil
    }
}

/// Mirrors Android's `GuestOrderViewModel`. The credentials live only here, for as long as the looked-up
/// booking is on screen — never in a route, saved state or log — and every async answer is checked
/// against a generation counter so a reply for a booking the guest has since edited away cannot land.
@MainActor
final class GuestOrderViewModel: ViewModel {
    static let maxReasonLength = 500

    @Published private(set) var state: GuestOrderUiState = .empty
    @Published private(set) var quote: UiState<CancellationQuote> = .loading
    @Published private(set) var cancelState: ActionState = .idle
    @Published private(set) var isCancellationPresented = false

    private let client: GuestOrderClient
    private let settings: AppSettingsStore
    private let localizer: ApiErrorLocalizing

    private var credentials: GuestOrderKey?
    private var generation = 0
    private var quoteGeneration = 0

    init(
        client: GuestOrderClient,
        settings: AppSettingsStore,
        localizer: ApiErrorLocalizing = ApiErrorLocalizer()
    ) {
        self.client = client
        self.settings = settings
        self.localizer = localizer
        super.init()
    }

    func clear() {
        generation += 1
        quoteGeneration += 1
        credentials = nil
        state = .empty
        quote = .loading
        cancelState = .idle
        isCancellationPresented = false
    }

    func onCredentialsChanged() {
        if !cancelState.isSubmitting { clear() }
    }

    func lookup(number: String, email: String, code: String) async {
        guard !cancelState.isSubmitting, state != .loading else { return }
        clear()
        guard let key = GuestOrderKey(number: number, email: email, code: code) else {
            state = .error(L10n.GuestOrder.required)
            return
        }
        credentials = key
        let current = generation
        state = .loading
        let result = await client.lookup(key)
        guard current == generation else { return }
        switch result {
        case let .success(order):
            state = .loaded(order)
        case let .failure(error):
            state = .error(localizer.message(for: error))
        }
    }

    func openCancellation() {
        guard let order = state.loadedOrder, order.isCancellable, !cancelState.isSubmitting else { return }
        isCancellationPresented = true
    }

    func dismissCancellation() {
        guard !cancelState.isSubmitting else { return }
        quoteGeneration += 1
        isCancellationPresented = false
        quote = .loading
        cancelState = .idle
    }

    /// A guest quote is refused unless it names the booking on screen in that booking's currency: the
    /// figures on the card are the only ones the guest will read before they commit.
    func loadQuote() async {
        guard isCancellationPresented, !cancelState.isSubmitting,
              let key = credentials, let order = state.loadedOrder
        else { return }
        let current = generation
        quoteGeneration += 1
        let currentQuote = quoteGeneration
        quote = .loading
        cancelState = .idle
        let result = await client.cancellationQuote(key)
        guard current == generation, currentQuote == quoteGeneration else { return }
        switch result {
        case let .success(received):
            let matchesOrder = received.orderId == order.id && received.quote.currencyCode == order.currencyCode
            quote = matchesOrder ? .loaded(received.quote) : .error(ApiError(code: Self.quoteMismatchCode))
        case let .failure(error):
            cancelState = .error(localizer.message(for: error))
            quote = .error(error)
        }
    }

    func cancel(reason: String?) async {
        guard isCancellationPresented, !cancelState.isSubmitting,
              let key = credentials, let order = state.loadedOrder, order.isCancellable,
              let reason, !reason.isBlank, reason.count <= Self.maxReasonLength,
              CancelOrderConfirmGate.quoteIsUsable(quote)
        else { return }
        let current = generation
        cancelState = .submitting
        let result = await client.cancel(key, reason: reason, language: settings.languageTag)
        guard current == generation else { return }
        switch result {
        case let .success(receipt):
            credentials = nil
            state = .cancelled(successMessage(receipt, currencyCode: order.currencyCode))
            isCancellationPresented = false
            cancelState = .idle
            quote = .loading
        case let .failure(error):
            cancelState = .error(localizer.message(for: error))
            quote = .error(error)
        }
    }

    private func successMessage(_ receipt: GuestOrderCancellation, currencyCode: String) -> String {
        guard let refunded = receipt.refunded else { return L10n.OrderCancel.successNoRefund }
        return L10n.OrderCancel.successWithRefund(OrdersFormat.price(refunded, currencyCode: currencyCode))
    }

    private static let quoteMismatchCode = "guest_order.quote_mismatch"
}
