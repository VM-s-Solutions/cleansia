import CleansiaCore
import Combine
import Foundation

@MainActor
final class BookingViewModel: ViewModel {
    @Published private(set) var state = BookingState()
    @Published private(set) var submitState: ActionState = .idle
    @Published private(set) var quoteState: BookingQuoteState = .idle
    @Published private(set) var promoState: PromoCodeState = .idle
    @Published private(set) var referralState: ReferralCodeState = .idle
    @Published private(set) var catalogState: UiState<Catalog> = .loading

    @Published private(set) var currentStep = 1

    private let catalogClient: CatalogClient
    private let quoteClient: QuoteClient
    private let quoteDebounce: DispatchQueue.SchedulerTimeType.Stride
    private let scheduler: AnySchedulerOf<DispatchQueue>

    private var lastQuoteRequest: QuoteRequest?
    private var quoteTask: Task<Void, Never>?
    private var cancellables = Set<AnyCancellable>()

    init(
        catalogClient: CatalogClient = LiveCatalogClient(),
        quoteClient: QuoteClient = LiveQuoteClient(),
        quoteDebounce: DispatchQueue.SchedulerTimeType.Stride = .milliseconds(400),
        scheduler: AnySchedulerOf<DispatchQueue> = .main
    ) {
        self.catalogClient = catalogClient
        self.quoteClient = quoteClient
        self.quoteDebounce = quoteDebounce
        self.scheduler = scheduler
        super.init()
        startQuoteWatcher()
    }

    var isFirstStep: Bool {
        currentStep <= 1
    }

    var isLastStep: Bool {
        currentStep >= BookingStepGate.totalSteps
    }

    func update(_ transform: (BookingState) -> BookingState) {
        state = transform(state)
    }

    @discardableResult
    func advance() -> Bool {
        guard currentStep < BookingStepGate.totalSteps else { return false }
        currentStep += 1
        return true
    }

    @discardableResult
    func back() -> Bool {
        guard currentStep > 1 else { return false }
        currentStep -= 1
        return true
    }

    func reset() {
        state = BookingState()
        submitState = .idle
        quoteState = .idle
        promoState = .idle
        referralState = .idle
        currentStep = 1
        lastQuoteRequest = nil
        quoteTask?.cancel()
    }

    func loadCatalog() async {
        if case .loaded = catalogState { return }
        await fetchCatalog()
    }

    func retryCatalog() async {
        catalogState = .loading
        await fetchCatalog()
    }

    private func fetchCatalog() async {
        catalogState = .loading
        switch await catalogClient.loadCatalog() {
        case let .success(catalog):
            catalogState = .loaded(catalog)
        case let .failure(error):
            catalogState = .error(error)
        }
    }

    private func startQuoteWatcher() {
        $state
            .map(\.quoteRequest)
            .removeDuplicates()
            .debounce(for: quoteDebounce, scheduler: scheduler)
            .sink { [weak self] request in
                self?.refreshQuote(for: request)
            }
            .store(in: &cancellables)
    }

    private func refreshQuote(for request: QuoteRequest) {
        quoteTask?.cancel()
        if request.serviceIds.isEmpty, request.packageIds.isEmpty {
            quoteState = .idle
            lastQuoteRequest = nil
            return
        }
        let previousQuote = quoteState.quote
        quoteState = .quoting
        quoteTask = Task { [weak self] in
            guard let self else { return }
            let result = await quoteClient.quote(request)
            if Task.isCancelled { return }
            switch result {
            case let .success(quote):
                lastQuoteRequest = request
                quoteState = .quoted(quote)
            case .failure:
                quoteState = previousQuote.map(BookingQuoteState.quoted) ?? .idle
            }
        }
    }
}

private extension BookingState {
    var quoteRequest: QuoteRequest {
        QuoteRequest(
            serviceIds: selectedServiceIds.sorted(),
            packageIds: selectedPackageIds.sorted(),
            extraSlugs: selectedExtraSlugs.sorted(),
            rooms: rooms,
            bathrooms: bathrooms,
            cleaningDate: selectedInstant
        )
    }
}
