import CleansiaCore
import Combine
import Foundation

@MainActor
final class RecurringBookingsViewModel: ViewModel {
    @Published private(set) var templates: [RecurringTemplate] = []
    @Published private(set) var loading = false
    @Published private(set) var loaded = false
    @Published private(set) var mutatingId: String?
    @Published private(set) var isPlusMember = false

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
        isPlusMember = membershipRepository.current?.hasMembership ?? false
        repository.$templates.assign(to: \.templates, on: self).store(in: &cancellables)
        repository.$loaded.assign(to: \.loaded, on: self).store(in: &cancellables)
        membershipRepository.$current
            .map { $0?.hasMembership ?? false }
            .assign(to: \.isPlusMember, on: self)
            .store(in: &cancellables)
    }

    func load() async {
        loading = true
        defer { loading = false }
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
}
