import CleansiaCore
import Combine
import Foundation

enum BookingEvent: Equatable {
    /// The address moved the draft into a market where part of the selection is not offered; the
    /// selection was cut down to what the reloaded catalogue still lists.
    case selectionPrunedForMarket
}

@MainActor
final class BookingViewModel: ViewModel {
    @Published private(set) var state = BookingState()
    @Published internal(set) var submitState: ActionState = .idle
    @Published internal(set) var quoteState: BookingQuoteState = .idle
    @Published internal(set) var promoState: PromoCodeState = .idle
    @Published internal(set) var referralState: ReferralCodeState = .idle
    @Published private(set) var catalogState: UiState<Catalog> = .loading
    @Published private(set) var extrasState: UiState<[CatalogExtra]> = .loading
    @Published private(set) var membership: MembershipSnapshot?
    @Published private(set) var expressWaiverStatus: ExpressWaiverStatus = .none
    /// Whether the account already holds the two consents the review step's tick names — Terms of
    /// Service and Privacy Policy. Re-consenting to the same two documents on every order is noise,
    /// so the box is shown only while this is false; it stays false on a failed read.
    @Published internal(set) var alreadyConsented = false
    /// The market the customer browses in — what the catalogue and the quote are priced for until
    /// an address decides otherwise.
    @Published private(set) var marketState: MarketState = .unavailable

    @Published private(set) var currentStep = 1

    let events = PassthroughSubject<BookingEvent, Never>()

    private let catalogClient: CatalogClient
    let quoteClient: QuoteClient
    private let membershipClient: MembershipClient
    private let extraClient: ExtraClient
    let promoClient: PromoCodeClient
    let referralClient: ReferralClient
    let profileClient: ProfileClient
    let orderCreateClient: OrderCreateClient
    let paymentIntentClient: PaymentIntentClient
    let countryResolver: CountryResolver
    let consentClient: ConsentStatusClient
    let tokenStore: TokenStore
    let isCardPaymentAvailable: Bool
    private let quoteDebounce: DispatchQueue.SchedulerTimeType.Stride
    private let scheduler: AnySchedulerOf<DispatchQueue>

    var lastQuoteRequest: QuoteRequest?
    private var quoteTask: Task<Void, Never>?
    private var catalogLoad: Task<Void, Never>?
    private var marketReload: Task<Void, Never>?
    private var countryLookup: Task<Void, Never>?
    private var membershipLoad: Task<MembershipSnapshot?, Never>?
    private var cancellables = Set<AnyCancellable>()

    init(
        catalogClient: CatalogClient = LiveCatalogClient(),
        quoteClient: QuoteClient = LiveQuoteClient(),
        membershipClient: MembershipClient = LiveMembershipClient(),
        extraClient: ExtraClient = LiveExtraClient(),
        promoClient: PromoCodeClient = LivePromoCodeClient(),
        referralClient: ReferralClient = LiveReferralClient(),
        profileClient: ProfileClient = LiveProfileClient(),
        orderCreateClient: OrderCreateClient = LiveOrderCreateClient(),
        paymentIntentClient: PaymentIntentClient = LivePaymentIntentClient(),
        countryResolver: CountryResolver = LiveCountryResolver(),
        consentClient: ConsentStatusClient = LiveConsentStatusClient(),
        tokenStore: TokenStore = CustomerBookingTokenStore.shared,
        market: AnyPublisher<MarketState, Never> = Just(.unavailable).eraseToAnyPublisher(),
        isCardPaymentAvailable: Bool = StripeConfig.isCardPaymentAvailable,
        quoteDebounce: DispatchQueue.SchedulerTimeType.Stride = .milliseconds(400),
        scheduler: AnySchedulerOf<DispatchQueue> = .main
    ) {
        self.catalogClient = catalogClient
        self.quoteClient = quoteClient
        self.membershipClient = membershipClient
        self.extraClient = extraClient
        self.promoClient = promoClient
        self.referralClient = referralClient
        self.profileClient = profileClient
        self.orderCreateClient = orderCreateClient
        self.paymentIntentClient = paymentIntentClient
        self.countryResolver = countryResolver
        self.consentClient = consentClient
        self.tokenStore = tokenStore
        self.isCardPaymentAvailable = isCardPaymentAvailable
        self.quoteDebounce = quoteDebounce
        self.scheduler = scheduler
        super.init()
        market.assign(to: &$marketState)
        startQuoteWatcher()
        startMarketWatcher()
    }

    /// Address > chosen market > default: the address's country once one is picked, the chosen
    /// market before that, and nothing (the platform default) when no market resolved.
    var catalogCountryId: String? {
        state.countryId ?? marketState.countryId
    }

    /// The insurance ceiling for the country the booking is priced in, in that country's currency;
    /// nil renders the no-figure claim.
    var insurance: MarketMoney? {
        marketState.insurance(forCountryId: catalogCountryId)
    }

    var isFirstStep: Bool {
        currentStep <= 1
    }

    /// Past the first step the sheet's leading control steps back instead of closing, and the
    /// swipe-down gesture is held so a half-built draft is not thrown away by a flick (Android
    /// intercepts the system back gesture the same way).
    var canStepBack: Bool {
        currentStep > 1
    }

    var isLastStep: Bool {
        currentStep >= BookingStepGate.totalSteps
    }

    var isQuoting: Bool {
        if case .quoting = quoteState { return true }
        return false
    }

    /// Waivers left this calendar month, as the server counted them. Never adjusted for the booking
    /// being composed — a client that decrements it disagrees with the server the first time a
    /// cancellation releases a slot.
    var expressUpgradesRemaining: Int {
        membership?.expressUpgradesRemaining ?? 0
    }

    /// The currency every wizard amount is labelled with. The quote's own code the moment one lands;
    /// until then the catalogue's default, which is what the pre-quote catalogue prices are stated in.
    /// Nil only before the catalogue has loaded, when there is no figure on screen to label.
    var displayCurrencyCode: String? {
        quoteState.quote?.currencyCode ?? catalogState.loadedValue?.currencyCode
    }

    /// Best of the server's own discounts and the promo code, the single input both the summary card
    /// and the sticky price bar subtract so they cannot show two different totals.
    var effectiveDiscount: Double {
        guard let quote = quoteState.quote else { return 0 }
        return max(quote.tierDiscountAmount + quote.membershipDiscountAmount, promoState.discount)
    }

    /// The tier floor this basket falls short of, shown only while no discount is winning — otherwise
    /// the hint contradicts the line above it (`ConfirmStep.kt` parity).
    var unmetTierDiscountFloor: Double? {
        guard effectiveDiscount == 0,
              let quote = quoteState.quote,
              let floor = quote.tierDiscountMinOrderAmount,
              floor > 0,
              quote.preSurchargeSubtotal < floor
        else { return nil }
        return floor
    }

    func update(_ transform: (BookingState) -> BookingState) {
        state = transform(state)
    }

    func setSpecialInstructions(_ value: String) {
        update { current in
            var next = current
            next.specialInstructions = BookingInstructions.capped(value)
            return next
        }
    }

    func setAccessInstructions(_ value: String) {
        update { current in
            var next = current
            next.accessInstructions = BookingInstructions.capped(value)
            return next
        }
    }

    @discardableResult
    func advance() -> Bool {
        guard currentStep < BookingStepGate.totalSteps else { return false }
        currentStep += 1
        return true
    }

    @discardableResult
    func back() -> Bool {
        guard canStepBack else { return false }
        currentStep -= 1
        return true
    }

    func reset() {
        state = BookingState()
        submitState = .idle
        membership = nil
        expressWaiverStatus = .none
        quoteState = .idle
        promoState = .idle
        referralState = .idle
        currentStep = 1
        lastQuoteRequest = nil
        quoteTask?.cancel()
        countryLookup?.cancel()
    }

    /// Single-flight: the shell prefetch and Home's catalog task can race at
    /// shell entry — the second caller joins the in-flight load instead of
    /// re-fetching (which would transiently flap `catalogState` back to loading).
    func loadCatalog() async {
        if case .loaded = catalogState { return }
        if let inFlight = catalogLoad {
            await inFlight.value
            return
        }
        let load = Task { await fetchCatalog() }
        catalogLoad = load
        await load.value
        catalogLoad = nil
    }

    func retryCatalog() async {
        catalogState = .loading
        await fetchCatalog()
    }

    private func fetchCatalog() async {
        catalogState = .loading
        let countryId = catalogCountryId
        switch await catalogClient.loadCatalog(countryId: countryId) {
        case let .success(catalog):
            catalogState = .loaded(catalog)
            if catalogCountryId != countryId {
                reloadCatalogForMarket(catalogCountryId)
            }
        case let .failure(error):
            catalogState = .error(error)
        }
    }

    /// The address step decides the market, and the chosen market does before there is an address:
    /// the catalogue is re-read priced for that country and the draft keeps only what it still
    /// lists. The catalogue on screen stays until the new one lands (or the reload fails), the way a
    /// re-quote keeps the previous total.
    private func startMarketWatcher() {
        Publishers.CombineLatest($state.map(\.countryId), $marketState.map(\.countryId))
            .map { address, market in address ?? market }
            .removeDuplicates()
            .dropFirst()
            .sink { [weak self] countryId in
                self?.reloadCatalogForMarket(countryId)
            }
            .store(in: &cancellables)
    }

    private func reloadCatalogForMarket(_ countryId: String?) {
        marketReload?.cancel()
        guard case .loaded = catalogState else { return }
        extrasState = .loading
        marketReload = Task { [weak self] in
            guard let self else { return }
            let result = await catalogClient.loadCatalog(countryId: countryId)
            if Task.isCancelled { return }
            guard case let .success(catalog) = result else { return }
            catalogState = .loaded(catalog)
            pruneSelection(notListedIn: catalog)
        }
    }

    private func pruneSelection(notListedIn catalog: Catalog) {
        let services = state.selectedServiceIds.intersection(catalog.services.map(\.id))
        let packages = state.selectedPackageIds.intersection(catalog.packages.map(\.id))
        guard services != state.selectedServiceIds || packages != state.selectedPackageIds else { return }
        update { current in
            var next = current
            next.selectedServiceIds = services
            next.selectedPackageIds = packages
            return next
        }
        events.send(.selectionPrunedForMarket)
    }

    /// The wizard's ONE read of the signed-in customer's membership — the slot grid's express-waiver
    /// note and the confirm step's cancellation policy share it rather than asking twice for the same
    /// answer. A guest is skipped and a failed read degrades to the same silence as "no membership":
    /// this enriches the most valuable screen in the product, so saying nothing beats a red toast.
    @discardableResult
    func loadMembership() async -> MembershipSnapshot? {
        if let membership { return membership }
        guard tokenStore.current() != nil else { return nil }
        if let inFlight = membershipLoad { return await inFlight.value }

        let load = Task { [membershipClient] () -> MembershipSnapshot? in
            guard case let .success(snapshot) = await membershipClient.currentMembership() else { return nil }
            return snapshot
        }
        membershipLoad = load
        let snapshot = await load.value
        membershipLoad = nil
        guard let snapshot else { return nil }
        membership = snapshot
        expressWaiverStatus = ExpressWaiverStatus.resolve(snapshot, now: Date())
        return snapshot
    }

    func loadExtras() async {
        if case .loaded = extrasState { return }
        switch await extraClient.loadExtras(countryId: catalogCountryId) {
        case let .success(extras):
            extrasState = .loaded(extras.sorted { $0.displayOrder < $1.displayOrder })
            let listed = Set(extras.map(\.slug))
            if !state.selectedExtraSlugs.isSubset(of: listed) {
                update { current in
                    var next = current
                    next.selectedExtraSlugs = current.selectedExtraSlugs.intersection(listed)
                    return next
                }
            }
        case let .failure(error):
            extrasState = .error(error)
        }
    }

    func toggleExtra(_ slug: String) {
        update { current in
            var next = current
            if next.selectedExtraSlugs.contains(slug) {
                next.selectedExtraSlugs.remove(slug)
            } else {
                next.selectedExtraSlugs.insert(slug)
            }
            return next
        }
    }

    func applyAddress(_ address: GeocodedAddress) {
        update { current in
            var next = current
            next.street = address.street.isBlank ? address.formatted : address.street
            next.city = address.city
            next.zipCode = address.zipCode
            next.countryIsoCode = address.countryIsoCode
            next.savedAddressId = nil
            next.hydratedFromSavedId = nil
            return next
        }
        resolveCountry(isoCode: address.countryIsoCode)
    }

    /// The market is written only once the country is known, so a same-country re-pick never flaps
    /// the catalogue through the default and back. A pick made in the meantime wins.
    private func resolveCountry(isoCode: String) {
        countryLookup?.cancel()
        countryLookup = Task { [weak self, countryResolver] in
            let resolved = await countryResolver.countryId(forIsoCode: isoCode)
            guard let self, !Task.isCancelled,
                  state.savedAddressId == nil, state.countryIsoCode == isoCode
            else { return }
            update { current in
                var next = current
                next.countryId = resolved
                return next
            }
        }
    }

    func selectDay(_ date: Date, calendar: Calendar = .current) {
        update { current in
            var next = current
            next.selectedDate = BookingDateFormat.dayLabel(date, calendar: calendar)
            let time = next.selectedTime
            next.selectedInstant = time.isBlank
                ? nil
                : BookingTimeSlots.instant(date: date, timeLabel: time, calendar: calendar)
            return next
        }
    }

    func selectTime(_ time: String, on date: Date, calendar: Calendar = .current) {
        update { current in
            var next = current
            next.selectedTime = time
            next.selectedInstant = BookingTimeSlots.instant(date: date, timeLabel: time, calendar: calendar)
            return next
        }
    }

    func clearSelectedTimeIfUnavailable(slots: [BookingTimeSlot]) {
        guard !state.selectedTime.isBlank else { return }
        let match = slots.first { $0.time == state.selectedTime }
        if match == nil || match?.state == .unavailable {
            update { current in
                var next = current
                next.selectedTime = ""
                next.selectedInstant = nil
                return next
            }
        }
    }

    private func startQuoteWatcher() {
        Publishers.CombineLatest($state, $marketState.map(\.countryId).removeDuplicates())
            .map { state, marketCountryId in state.quoteRequest(marketCountryId: marketCountryId) }
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
            // Cache invalidation, not tidying: without this a later submit of
            // the same request shape could be served the abandoned quote.
            lastQuoteRequest = nil
            return
        }
        let previousQuote = quoteState.quote
        // Hand the outgoing quote to `.quoting` so the summary keeps the last
        // known total for the duration of the round trip.
        quoteState = .quoting(previous: previousQuote)
        quoteTask = Task { [weak self] in
            guard let self else { return }
            let result = await quoteClient.quote(request)
            if Task.isCancelled { return }
            switch result {
            case let .success(quote):
                lastQuoteRequest = request
                quoteState = .quoted(quote)
                if let previousQuote, previousQuote.currencyId != quote.currencyId, case .valid = promoState {
                    clearPromoCode()
                }
            case .failure:
                quoteState = previousQuote.map(BookingQuoteState.quoted) ?? .idle
            }
        }
    }
}

extension BookingState {
    /// The quote is priced for the address's country once there is one, else the chosen market's.
    func quoteRequest(marketCountryId: String?) -> QuoteRequest {
        QuoteRequest(
            serviceIds: selectedServiceIds.sorted(),
            packageIds: selectedPackageIds.sorted(),
            extraSlugs: selectedExtraSlugs.sorted(),
            rooms: rooms,
            bathrooms: bathrooms,
            cleaningDate: selectedInstant,
            countryId: countryId ?? marketCountryId
        )
    }
}
