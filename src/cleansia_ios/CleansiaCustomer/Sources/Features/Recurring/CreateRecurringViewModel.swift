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

@MainActor
final class CreateRecurringViewModel: ViewModel {
    @Published private(set) var formState = CreateRecurringFormState()
    @Published private(set) var submitState: ActionState = .idle
    @Published private(set) var catalog: Catalog = .empty
    @Published private(set) var savedAddresses: [RecurringSavedAddress] = []

    let sourceOrderId: String?
    let editing: RecurringTemplate?

    private let repository: RecurringBookingRepository
    private let catalogClient: CatalogClient
    private let addressClient: RecurringSavedAddressClient
    private let orderClient: OrderClient
    private let snackbar: SnackbarController

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
    }

    var isEditing: Bool {
        editing != nil
    }

    var isValid: Bool {
        formState.isValid
    }

    /// An edited template can start in the past; a new one cannot start before today.
    var earliestStart: Date {
        guard let startsOn = editing?.startsOn else { return Date() }
        return min(startsOn, Date())
    }

    func load() async {
        async let catalogResult = catalogClient.loadCatalog()
        async let addressResult = addressClient.getMine()

        if case let .success(catalog) = await catalogResult {
            self.catalog = catalog
        }
        if case let .success(addresses) = await addressResult {
            apply(addresses)
        }
        if let sourceOrderId {
            await prefill(from: sourceOrderId)
        }
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
        state.rooms = max(0, order.rooms ?? state.rooms)
        state.bathrooms = max(0, order.bathrooms ?? state.bathrooms)
        state.selectedServiceIds = Set((order.selectedServices ?? []).compactMap(\.id))
        state.selectedPackageIds = Set((order.selectedPackages ?? []).compactMap(\.id))
        if let paymentType = order.paymentType?.value {
            state.paymentType = paymentType
        }
        if let cleaningDate = order.cleaningDateTime {
            state.timeOfDay = RecurringTime.format(cleaningDate)
            state.dayOfWeek = RecurringTime.dotNetDayOfWeek(cleaningDate)
        }
        formState = state
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
