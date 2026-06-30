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
}

@MainActor
final class CreateRecurringViewModel: ViewModel {
    @Published private(set) var formState = CreateRecurringFormState()
    @Published private(set) var submitState: ActionState = .idle
    @Published private(set) var catalog: Catalog = .empty
    @Published private(set) var savedAddresses: [RecurringSavedAddress] = []

    let sourceOrderId: String?

    private let repository: RecurringBookingRepository
    private let catalogClient: CatalogClient
    private let addressClient: RecurringSavedAddressClient
    private let orderClient: OrderClient
    private let snackbar: SnackbarController

    init(
        sourceOrderId: String?,
        repository: RecurringBookingRepository,
        catalogClient: CatalogClient,
        addressClient: RecurringSavedAddressClient,
        orderClient: OrderClient,
        snackbar: SnackbarController
    ) {
        self.sourceOrderId = sourceOrderId?.isBlank == false ? sourceOrderId : nil
        self.repository = repository
        self.catalogClient = catalogClient
        self.addressClient = addressClient
        self.orderClient = orderClient
        self.snackbar = snackbar
        super.init()
    }

    var isValid: Bool {
        formState.isValid
    }

    func load() async {
        async let catalogResult = catalogClient.loadCatalog()
        async let addressResult = addressClient.getMine()

        if case let .success(catalog) = await catalogResult {
            self.catalog = catalog
        }
        if case let .success(addresses) = await addressResult {
            savedAddresses = addresses
            if formState.savedAddressId.isBlank {
                let preferred = addresses.first(where: \.isDefault) ?? addresses.first
                if let preferred {
                    formState.savedAddressId = preferred.id
                }
            }
        }
        if let sourceOrderId {
            await prefill(from: sourceOrderId)
        }
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
        switch await repository.create(input) {
        case .success:
            submitState = .idle
            snackbar.showSuccess(L10n.Recurring.createSuccess)
            return true
        case let .failure(error):
            snackbar.showApiError(error)
            submitState = .error(L10n.Recurring.createFailed)
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
