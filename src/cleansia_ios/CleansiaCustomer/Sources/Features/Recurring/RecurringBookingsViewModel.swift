import CleansiaCore
import Combine
import Foundation

/// Mirrors the server's split. Authoring a schedule — `CreateRecurringBooking`,
/// `UpdateRecurringBooking` — is the paid Cleansia Plus capability. Listing, pausing,
/// resuming and deleting one that already exists is deliberately ungated so a lapsed
/// subscriber can always stop what is still generating billable cleanings.
///
/// `paused` is a live enrolment whose benefits are paused (past due or paused): the server
/// refuses authoring and books none of its schedules, yet refuses a second subscription too,
/// so it gets neither the create affordances nor the subscribe upsell — Android's `Paused`.
enum RecurringAuthoringGate {
    case allowed
    case upsell
    case paused

    /// A nil `hasMembership` is the answer not having landed. It resolves permissively:
    /// the server refuses an unentitled create on its own, so failing open costs a member
    /// nothing, while failing closed shows a paid-up member the upsell every time the
    /// fetch is slow or fails.
    static func resolve(hasMembership: Bool?, benefitsPaused: Bool = false) -> RecurringAuthoringGate {
        switch hasMembership {
        case nil: .allowed
        case false?: .upsell
        case true?: benefitsPaused ? .paused : .allowed
        }
    }
}

struct RecurringListAffordances: Equatable {
    let showCreateAction: Bool
    let showPlusUpsell: Bool
    let showLapsedNotice: Bool
    let showPausedNotice: Bool
    let showEdit: Bool

    static func of(gate: RecurringAuthoringGate, hasTemplates: Bool) -> RecurringListAffordances {
        RecurringListAffordances(
            showCreateAction: gate == .allowed && hasTemplates,
            showPlusUpsell: gate == .upsell && !hasTemplates,
            showLapsedNotice: gate == .upsell && hasTemplates,
            showPausedNotice: gate == .paused,
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
    /// False until the membership lands, so a slow fetch fails open as `hasMembership` does.
    @Published private(set) var benefitsPaused = false
    /// A schedule is priced in its saved address's country; nil until the addresses land.
    @Published private var addresses: [RecurringSavedAddress]?
    /// What each market's catalogue lists today, keyed by country (nil: the platform default).
    @Published private var catalogues: [String?: Catalog] = [:]

    private let repository: RecurringBookingRepository
    private let membershipRepository: MembershipRepository
    private let addressClient: RecurringSavedAddressClient
    private let catalogClient: CatalogClient
    private let snackbar: SnackbarController

    init(
        repository: RecurringBookingRepository,
        membershipRepository: MembershipRepository,
        addressClient: RecurringSavedAddressClient,
        catalogClient: CatalogClient,
        snackbar: SnackbarController
    ) {
        self.repository = repository
        self.membershipRepository = membershipRepository
        self.addressClient = addressClient
        self.catalogClient = catalogClient
        self.snackbar = snackbar
        super.init()
        repository.$templates.assign(to: &$templates)
        repository.$loaded.assign(to: &$loaded)
        membershipRepository.$current
            .map { $0?.hasMembership }
            .assign(to: &$hasMembership)
        membershipRepository.$current
            .map { $0?.benefitsPaused ?? false }
            .assign(to: &$benefitsPaused)
    }

    var authoring: RecurringAuthoringGate {
        .resolve(hasMembership: hasMembership, benefitsPaused: benefitsPaused)
    }

    var affordances: RecurringListAffordances {
        .of(gate: authoring, hasTemplates: !templates.isEmpty)
    }

    /// The schedules holding a service or package their market's catalogue no longer lists. A schedule
    /// whose market's catalogue has not been read is not judged: the card says nothing rather than guess.
    var noLongerOfferedIds: Set<String> {
        Set(templates.filter { template in
            guard let address = addresses?.first(where: { $0.id == template.savedAddressId }),
                  let catalog = catalogues[address.countryId]
            else { return false }
            return !Set(template.selectedServiceIds).isSubset(of: catalog.services.map(\.id))
                || !Set(template.selectedPackageIds).isSubset(of: catalog.packages.map(\.id))
        }.map(\.id))
    }

    func load() async {
        loading = true
        defer { loading = false }
        await refreshMembership()
        if case let .failure(error) = await repository.refresh() {
            snackbar.showApiError(error)
        }
        await readCatalogues()
    }

    /// Re-read on every visit, one catalogue per market the schedules are priced in. A read that fails
    /// leaves what was last read; a market never read stays unjudged. Nothing here is the customer's
    /// to act on, so a failure is silent.
    private func readCatalogues() async {
        guard !templates.isEmpty, case let .success(list) = await addressClient.getMine() else { return }
        addresses = list
        let markets = Set(templates.compactMap { template in
            list.first { $0.id == template.savedAddressId }
        }.map(\.countryId))
        for countryId in markets {
            if case let .success(catalog) = await catalogClient.loadCatalog(countryId: countryId) {
                catalogues[countryId] = catalog
            }
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
