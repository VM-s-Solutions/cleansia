import CleansiaCore
import Combine
import Foundation

/// Mirrors the server's split. Authoring a schedule — `CreateRecurringBooking`,
/// `UpdateRecurringBooking` — is the paid Cleansia Plus capability. Listing, pausing,
/// resuming and deleting one that already exists is deliberately ungated so a lapsed
/// subscriber can always stop what is still generating billable cleanings.
enum RecurringAuthoringGate {
    case allowed
    case upsell

    /// A nil `hasMembership` is the answer not having landed. It resolves permissively:
    /// the server refuses an unentitled create on its own, so failing open costs a member
    /// nothing, while failing closed shows a paid-up member the upsell every time the
    /// fetch is slow or fails.
    static func resolve(hasMembership: Bool?) -> RecurringAuthoringGate {
        hasMembership == false ? .upsell : .allowed
    }
}

struct RecurringListAffordances: Equatable {
    let showCreateAction: Bool
    let showPlusUpsell: Bool
    let showLapsedNotice: Bool
    let showEdit: Bool

    static func of(gate: RecurringAuthoringGate, hasTemplates: Bool) -> RecurringListAffordances {
        RecurringListAffordances(
            showCreateAction: gate == .allowed && hasTemplates,
            showPlusUpsell: gate == .upsell && !hasTemplates,
            showLapsedNotice: gate == .upsell && hasTemplates,
            showEdit: gate == .allowed
        )
    }
}

@MainActor
final class RecurringBookingsViewModel: ViewModel {
    @Published private(set) var templates: [RecurringTemplate] = []
    @Published private(set) var loading = false
    @Published private(set) var loaded = false
    @Published private(set) var mutatingId: String?
    @Published private(set) var hasMembership: Bool?

    private let repository: RecurringBookingRepository
    private let membershipRepository: MembershipRepository
    private let snackbar: SnackbarController
    private var cancellables: Set<AnyCancellable> = []

    init(
        repository: RecurringBookingRepository,
        membershipRepository: MembershipRepository,
        snackbar: SnackbarController
    ) {
        self.repository = repository
        self.membershipRepository = membershipRepository
        self.snackbar = snackbar
        super.init()
        templates = repository.templates
        loaded = repository.loaded
        hasMembership = membershipRepository.current?.hasMembership
        repository.$templates.assign(to: \.templates, on: self).store(in: &cancellables)
        repository.$loaded.assign(to: \.loaded, on: self).store(in: &cancellables)
        membershipRepository.$current
            .map { $0?.hasMembership }
            .assign(to: \.hasMembership, on: self)
            .store(in: &cancellables)
    }

    var authoring: RecurringAuthoringGate {
        .resolve(hasMembership: hasMembership)
    }

    var affordances: RecurringListAffordances {
        .of(gate: authoring, hasTemplates: !templates.isEmpty)
    }

    func load() async {
        loading = true
        defer { loading = false }
        await refreshMembership()
        if case let .failure(error) = await repository.refresh() {
            snackbar.showApiError(error)
        }
    }

    func toggleActive(templateId: String, currentlyActive: Bool) async {
        guard mutatingId == nil else { return }
        mutatingId = templateId
        defer { mutatingId = nil }
        if case let .failure(error) = await repository.setActive(templateId: templateId, isActive: !currentlyActive) {
            snackbar.showApiError(error)
        }
    }

    func delete(templateId: String) async {
        guard mutatingId == nil else { return }
        mutatingId = templateId
        defer { mutatingId = nil }
        if case let .failure(error) = await repository.delete(templateId: templateId) {
            snackbar.showApiError(error)
        }
    }

    /// A screen that gates on membership fetches it. Reading whatever another screen
    /// happened to warm is what put the upsell wall in front of paid-up members on
    /// cold entry. Failure leaves `hasMembership` nil, which fails open.
    private func refreshMembership() async {
        guard membershipRepository.staleness.isStale else { return }
        await membershipRepository.refresh()
    }
}
