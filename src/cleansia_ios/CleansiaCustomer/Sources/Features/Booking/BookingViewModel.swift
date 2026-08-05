import CleansiaCore
import Combine
import Foundation

@MainActor
final class BookingViewModel: ViewModel {
    @Published private(set) var state = BookingState()
    @Published internal(set) var submitState: ActionState = .idle
    @Published internal(set) var quoteState: BookingQuoteState = .idle
    @Published private(set) var promoState: PromoCodeState = .idle
    @Published private(set) var referralState: ReferralCodeState = .idle
    @Published private(set) var catalogState: UiState<Catalog> = .loading
    @Published private(set) var extrasState: UiState<[CatalogExtra]> = .loading
    @Published private(set) var membership: MembershipSnapshot?
    @Published private(set) var expressWaiverStatus: ExpressWaiverStatus = .none

    @Published private(set) var currentStep = 1

    private let catalogClient: CatalogClient
    let quoteClient: QuoteClient
    private let membershipClient: MembershipClient
    private let extraClient: ExtraClient
    private let promoClient: PromoCodeClient
    private let referralClient: ReferralClient
    let profileClient: ProfileClient
    let orderCreateClient: OrderCreateClient
    let paymentIntentClient: PaymentIntentClient
    let countryResolver: CountryResolver
    let tokenStore: TokenStore
    let isCardPaymentAvailable: Bool
    private let quoteDebounce: DispatchQueue.SchedulerTimeType.Stride
    private let scheduler: AnySchedulerOf<DispatchQueue>

    var lastQuoteRequest: QuoteRequest?
    private var quoteTask: Task<Void, Never>?
    private var catalogLoad: Task<Void, Never>?
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
        tokenStore: TokenStore = CustomerBookingTokenStore.shared,
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
        self.tokenStore = tokenStore
        self.isCardPaymentAvailable = isCardPaymentAvailable
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

    /// Best of the server's own discounts and the promo code, the single input both the summary card
    /// and the sticky price bar subtract so they cannot show two different totals.
    var effectiveDiscount: Double {
        guard let quote = quoteState.quote else { return 0 }
        return max(quote.tierDiscountAmount + quote.membershipDiscountAmount, promoState.discount)
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
        guard currentStep > 1 else { return false }
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
        switch await catalogClient.loadCatalog() {
        case let .success(catalog):
            catalogState = .loaded(catalog)
        case let .failure(error):
            catalogState = .error(error)
        }
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
        switch await extraClient.loadExtras() {
        case let .success(extras):
            extrasState = .loaded(extras.sorted { $0.displayOrder < $1.displayOrder })
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

    @discardableResult
    func validatePromoCode(_ rawCode: String) async -> PromoCodeState {
        let normalized = rawCode.trimmingCharacters(in: .whitespacesAndNewlines).uppercased()
        if normalized.isEmpty {
            promoState = .idle
            return .idle
        }
        promoState = .validating
        let subtotal = quoteState.quote?.totalPrice ?? 0
        let resolved: PromoCodeState = switch await promoClient.validate(code: normalized, orderSubtotal: subtotal) {
        case let .success(validation):
            if validation.isValid, let discount = validation.discountAmount {
                .valid(discountAmount: discount)
            } else {
                .invalid(PromoCodeError.from(validation.errorCode))
            }
        case .failure:
            .invalid(nil)
        }
        promoState = resolved
        if case .valid = resolved {
            update { current in
                var next = current
                next.promoCode = normalized
                return next
            }
        }
        return resolved
    }

    @discardableResult
    func validateReferralCode(_ rawCode: String) async -> ReferralCodeState {
        let normalized = rawCode.trimmingCharacters(in: .whitespacesAndNewlines).uppercased()
        if normalized.isEmpty {
            referralState = .idle
            return .idle
        }
        referralState = .validating
        let resolved: ReferralCodeState = switch await referralClient.validate(code: normalized) {
        case let .success(validation):
            if validation.isValid {
                .valid(referrerFirstName: validation.referrerFirstName)
            } else {
                .invalid(ReferralValidationError.from(validation.errorCode))
            }
        case .failure:
            .invalid(nil)
        }
        referralState = resolved
        if case .valid = resolved {
            update { current in
                var next = current
                next.referralCode = normalized
                return next
            }
        }
        return resolved
    }

    func clearPromoCode() {
        promoState = .idle
        update { current in
            var next = current
            next.promoCode = ""
            return next
        }
    }

    func clearReferralCode() {
        referralState = .idle
        update { current in
            var next = current
            next.referralCode = ""
            return next
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
            case .failure:
                quoteState = previousQuote.map(BookingQuoteState.quoted) ?? .idle
            }
        }
    }
}

extension BookingState {
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
