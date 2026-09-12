import CleansiaCore
import Combine
import Foundation

struct CreateRecurringFormState: Equatable {
    var frequency: RecurrenceFrequency = .weekly
    var dayOfWeek = 4
    var timeOfDay = "10:00"
    var rooms = 2
    var bathrooms = 1
    var savedAddressId = ""
    var selectedServiceIds: Set<String> = []
    var selectedPackageIds: Set<String> = []
    var paymentType = 1
    var startsOn: Date?

    var isValid: Bool {
        !savedAddressId.isBlank
            && (!selectedServiceIds.isEmpty || !selectedPackageIds.isEmpty)
            && startsOn != nil
            && !timeOfDay.isBlank
    }

    init() {}

    init(_ template: RecurringTemplate) {
        frequency = RecurrenceFrequency(rawValue: template.frequency) ?? .weekly
        dayOfWeek = template.dayOfWeek
        timeOfDay = template.timeOfDay
        rooms = template.rooms
        bathrooms = template.bathrooms
        savedAddressId = template.savedAddressId
        selectedServiceIds = Set(template.selectedServiceIds)
        selectedPackageIds = Set(template.selectedPackageIds)
        paymentType = template.paymentType
        startsOn = template.startsOn
    }
}

extension UpdateRecurringInput {
    init(_ input: CreateRecurringInput, templateId: String, endsOn: Date?) {
        self.init(
            templateId: templateId,
            frequency: input.frequency,
            dayOfWeek: input.dayOfWeek,
            timeOfDay: input.timeOfDay,
            rooms: input.rooms,
            bathrooms: input.bathrooms,
            savedAddressId: input.savedAddressId,
            selectedServiceIds: input.selectedServiceIds,
            selectedPackageIds: input.selectedPackageIds,
            paymentType: input.paymentType,
            startsOn: input.startsOn,
            endsOn: endsOn
        )
    }
}

enum CreateRecurringEvent: Equatable {
    /// Part of the selection is not offered in the picked address's market — the address moved the
    /// schedule there, or the order it was prefilled from was priced elsewhere; the selection was
    /// cut down to what the catalogue for that market lists.
    case selectionPrunedForMarket
}

@MainActor
final class CreateRecurringViewModel: ViewModel {
    @Published private(set) var formState = CreateRecurringFormState()
    @Published private(set) var submitState: ActionState = .idle
    @Published private(set) var catalog: Catalog = .empty
    @Published private(set) var savedAddresses: [RecurringSavedAddress] = []

    let sourceOrderId: String?
    let editing: RecurringTemplate?
    let events = PassthroughSubject<CreateRecurringEvent, Never>()

    private let repository: RecurringBookingRepository
    private let catalogClient: CatalogClient
    private let addressClient: RecurringSavedAddressClient
    private let orderClient: OrderClient
    private let snackbar: SnackbarController
    private var isCatalogLoaded = false
    private var catalogCountryId: String?
    private var marketReload: Task<Void, Never>?
    private var cancellables = Set<AnyCancellable>()

    init(
        sourceOrderId: String?,
        editing: RecurringTemplate? = nil,
        repository: RecurringBookingRepository,
        catalogClient: CatalogClient,
        addressClient: RecurringSavedAddressClient,
        orderClient: OrderClient,
        snackbar: SnackbarController
    ) {
        self.sourceOrderId = editing == nil && sourceOrderId?.isBlank == false ? sourceOrderId : nil
        self.editing = editing
        self.repository = repository
        self.catalogClient = catalogClient
        self.addressClient = addressClient
        self.orderClient = orderClient
        self.snackbar = snackbar
        super.init()
        if let editing {
            formState = CreateRecurringFormState(editing)
        }
        startMarketWatcher()
    }

    var isEditing: Bool {
        editing != nil
    }

    var isValid: Bool {
        formState.isValid
    }

    /// `UpdateSchedule` rewrites the template and clears the materialisation watermark, so the new
    /// schedule takes effect from now — but it deletes no orders, and the sweep only ever computes
    /// occurrences at or after `now`. Cleanings already on the calendar therefore keep the slot they
    /// were booked into, and the only way to be rid of them is to cancel them one by one.
    var appliesNotice: String? {
        isEditing ? L10n.Recurring.editAppliesNotice : nil
    }

    /// An edited template can start in the past; a new one cannot start before today.
    var earliestStart: Date {
        guard let startsOn = editing?.startsOn else { return Date() }
        return min(startsOn, Date())
    }

    /// The country of the picked saved address — the market the schedule is priced in.
    var selectedCountryId: String? {
        savedAddresses.first { $0.id == formState.savedAddressId }?.countryId
    }

    /// The addresses come first so the catalogue is read once, priced for the seeded address's market,
    /// and the prefill last so the order's picks are pruned against that catalogue.
    func load() async {
        if case let .success(addresses) = await addressClient.getMine() {
            apply(addresses)
        }
        await fetchCatalog()
        if let sourceOrderId {
            await prefill(from: sourceOrderId)
        }
    }

    private func fetchCatalog() async {
        let countryId = selectedCountryId
        guard case let .success(catalog) = await catalogClient.loadCatalog(countryId: countryId) else { return }
        self.catalog = catalog
        isCatalogLoaded = true
        catalogCountryId = countryId
        if selectedCountryId != countryId {
            reloadCatalogForMarket(selectedCountryId)
        }
    }

    /// The picked address decides the market, the way the booking wizard's address step does: the
    /// catalogue is re-read priced for that country and the selection keeps only what it still lists.
    /// The catalogue on screen stays until the new one lands (or the reload fails). An id the list
    /// does not know yet (one just created inline) is waited out rather than flapping the market
    /// through the default and back.
    private func startMarketWatcher() {
        Publishers.CombineLatest($formState.map(\.savedAddressId), $savedAddresses)
            .compactMap { savedAddressId, addresses in addresses.first { $0.id == savedAddressId } }
            .map(\.countryId)
            .removeDuplicates()
            .sink { [weak self] countryId in
                self?.reloadCatalogForMarket(countryId)
            }
            .store(in: &cancellables)
    }

    private func reloadCatalogForMarket(_ countryId: String?) {
        marketReload?.cancel()
        guard isCatalogLoaded, countryId != catalogCountryId else { return }
        marketReload = Task { [weak self] in
            guard let self else { return }
            let result = await catalogClient.loadCatalog(countryId: countryId)
            if Task.isCancelled { return }
            guard case let .success(catalog) = result else { return }
            self.catalog = catalog
            catalogCountryId = countryId
            pruneSelection(notListedIn: catalog)
        }
    }

    /// A market reload still in flight prunes when it lands; pruning against the catalogue it is
    /// replacing would drop what the new market may well price.
    private var isCatalogForSelectedMarket: Bool {
        isCatalogLoaded && catalogCountryId == selectedCountryId
    }

    private func pruneSelection(notListedIn catalog: Catalog) {
        let services = formState.selectedServiceIds.intersection(catalog.services.map(\.id))
        let packages = formState.selectedPackageIds.intersection(catalog.packages.map(\.id))
        guard services != formState.selectedServiceIds || packages != formState.selectedPackageIds else { return }
        formState.selectedServiceIds = services
        formState.selectedPackageIds = packages
        events.send(.selectionPrunedForMarket)
    }

    /// Re-read the list after the inline address manager closes — an address
    /// created there is invisible to this form's `load()` snapshot, so the row
    /// the customer just made would not be there to pick.
    func reloadAddresses() async {
        if case let .success(addresses) = await addressClient.getMine() {
            apply(addresses)
        }
    }

    /// Seeding only fills a blank selection, so a hand-picked address survives
    /// a reload that a newly-added default would otherwise steal.
    private func apply(_ addresses: [RecurringSavedAddress]) {
        savedAddresses = addresses
        guard formState.savedAddressId.isBlank,
              let preferred = addresses.first(where: \.isDefault) ?? addresses.first
        else { return }
        formState.savedAddressId = preferred.id
    }

    // MARK: - Mutators

    func setFrequency(_ frequency: RecurrenceFrequency) {
        formState.frequency = frequency
    }

    func setDayOfWeek(_ day: Int) {
        formState.dayOfWeek = day
    }

    func setTimeOfDay(_ time: String) {
        formState.timeOfDay = time
    }

    func setRooms(_ count: Int) {
        formState.rooms = max(0, count)
    }

    func setBathrooms(_ count: Int) {
        formState.bathrooms = max(0, count)
    }

    func setSavedAddressId(_ id: String) {
        formState.savedAddressId = id
    }

    func setPaymentType(_ type: Int) {
        formState.paymentType = type
    }

    func setStartsOn(_ date: Date) {
        formState.startsOn = date
    }

    func toggleService(_ id: String) {
        if formState.selectedServiceIds.contains(id) {
            formState.selectedServiceIds.remove(id)
        } else {
            formState.selectedServiceIds.insert(id)
        }
    }

    func togglePackage(_ id: String) {
        if formState.selectedPackageIds.contains(id) {
            formState.selectedPackageIds.remove(id)
        } else {
            formState.selectedPackageIds.insert(id)
        }
    }

    // MARK: - Submit

    func submit() async -> Bool {
        guard !submitState.isSubmitting else { return false }
        guard let input = buildInput() else { return false }
        submitState = .submitting
        let result: ApiResult<RecurringTemplate> = if let editing {
            await repository.update(UpdateRecurringInput(input, templateId: editing.id, endsOn: editing.endsOn))
        } else {
            await repository.create(input)
        }
        switch result {
        case .success:
            submitState = .idle
            snackbar.showSuccess(isEditing ? L10n.Recurring.editSuccess : L10n.Recurring.createSuccess)
            return true
        case let .failure(error):
            snackbar.showApiError(error)
            submitState = .error(isEditing ? L10n.Recurring.editFailed : L10n.Recurring.createFailed)
            return false
        }
    }

    private func buildInput() -> CreateRecurringInput? {
        let state = formState
        guard !state.savedAddressId.isBlank,
              !state.selectedServiceIds.isEmpty || !state.selectedPackageIds.isEmpty,
              let startsOn = state.startsOn,
              !state.timeOfDay.isBlank
        else { return nil }
        return CreateRecurringInput(
            frequency: state.frequency.rawValue,
            dayOfWeek: state.dayOfWeek,
            timeOfDay: state.timeOfDay,
            rooms: state.rooms,
            bathrooms: state.bathrooms,
            savedAddressId: state.savedAddressId,
            selectedServiceIds: Array(state.selectedServiceIds),
            selectedPackageIds: Array(state.selectedPackageIds),
            paymentType: state.paymentType,
            startsOn: startsOn
        )
    }

    private func prefill(from orderId: String) async {
        guard case let .success(order) = await orderClient.getById(orderId: orderId) else { return }
        var state = formState
        state.rooms = max(0, order.rooms)
        state.bathrooms = max(0, order.bathrooms)
        state.selectedServiceIds = Set(order.services.compactMap(\.id))
        state.selectedPackageIds = Set(order.packages.compactMap(\.id))
        if let paymentType = order.paymentType?.value {
            state.paymentType = paymentType
        }
        if let cleaningDate = order.cleaningDateTime {
            state.timeOfDay = RecurringTime.format(cleaningDate)
            state.dayOfWeek = RecurringTime.dotNetDayOfWeek(cleaningDate)
        }
        formState = state
        if isCatalogForSelectedMarket {
            pruneSelection(notListedIn: catalog)
        }
    }
}

enum RecurringTime {
    static func format(_ date: Date) -> String {
        let components = Calendar.current.dateComponents([.hour, .minute], from: date)
        return String(format: "%02d:%02d", components.hour ?? 0, components.minute ?? 0)
    }

    /// Foundation weekday: Sun=1..Sat=7. Backend wants .NET DayOfWeek: Sun=0..Sat=6.
    static func dotNetDayOfWeek(_ date: Date) -> Int {
        let weekday = Calendar.current.component(.weekday, from: date)
        return (weekday - 1) % 7
    }
}
