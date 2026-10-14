import CleansiaCore
import CleansiaPartnerApi
import Foundation

@MainActor
final class DashboardViewModel: ViewModel {
    @Published private(set) var state: UiState<DashboardData> = .loading
    @Published private(set) var showsJobRadiusPrompt = false

    private let client: PartnerDashboardClient
    private let settings: AppSettingsStore
    private let staleness: OrdersStaleness
    private var employeeId: String?
    private var fetchGeneration = 0

    init(client: PartnerDashboardClient, settings: AppSettingsStore, staleness: OrdersStaleness) {
        self.client = client
        self.settings = settings
        self.staleness = staleness
    }

    /// A loaded dashboard is kept for a minute, as Android keeps it, so a return to the tab reads nothing; after
    /// that the return refreshes it in place, behind no spinner.
    func load() async {
        guard !Task.isCancelled else { return }
        if state.loadedValue != nil {
            guard staleness.isDashboardStale else { return }
        } else {
            state = .loading
        }
        await fetch(preservingLoadedOnFailure: true)
    }

    /// A pull already has a spinner — the one under the cleaner's finger. Flipping to `.loading` would
    /// replace the whole dashboard with a second one and throw away the numbers they were looking at,
    /// so this reloads in place and leaves the last good state visible until the new one arrives. The
    /// Android twin draws the same distinction with `isUserRefreshing`.
    ///
    /// A failure still lands on `.error`, exactly as the initial load does: a pull that silently kept
    /// stale figures would be worse than one that says it could not refresh them.
    func userRefresh() async {
        await fetch(preservingLoadedOnFailure: false)
    }

    private func fetch(preservingLoadedOnFailure: Bool) async {
        guard !Task.isCancelled else { return }
        fetchGeneration += 1
        let generation = fetchGeneration
        let revision = staleness.dashboardRevision
        async let previewResult = client.getAvailableJobsPreview(limit: Self.previewLimit)
        let employee = try? await client.getCurrentEmployee().get()
        guard !Task.isCancelled, generation == fetchGeneration, revision == staleness.dashboardRevision else { return }
        let capturedEmployeeId = employee?.id
        async let upcoming = upcomingOrders(for: capturedEmployeeId)
        let nextState: UiState<DashboardData>

        switch await client.getStats(employeeId: capturedEmployeeId) {
        case let .success(stats):
            let preview = try? await previewResult.get()
            nextState = await .loaded(DashboardData.from(
                stats: stats,
                preview: preview,
                upcoming: upcoming,
                firstName: employee?.firstName
            ))
        case let .failure(error):
            if preservingLoadedOnFailure, state.loadedValue != nil { return }
            nextState = .error(error)
        }

        guard !Task.isCancelled, generation == fetchGeneration, revision == staleness.dashboardRevision else { return }
        employeeId = capturedEmployeeId
        state = nextState
        if nextState.loadedValue != nil { staleness.markDashboardFresh() }
        resolveJobRadiusPrompt(radiusKm: employee?.jobRadiusKm, employeeRead: employee != nil)
    }

    private func upcomingOrders(for employeeId: String?) async -> [OrderListItem]? {
        guard let employeeId, !employeeId.isBlank else { return nil }
        return try? await client.getUpcomingOrders(employeeId: employeeId, limit: Self.upcomingLimit).get()
    }

    /// Both answers are answers, so either one spends the ask. A cleaner who taps through to the
    /// screen and backs out without saving keeps the country-wide board, which is what they had.
    func answerJobRadiusPrompt() {
        showsJobRadiusPrompt = false
        guard let employeeId, !employeeId.isBlank else { return }
        settings.markPromptAnswered(JobRadiusPrompt.settingsKey, userId: employeeId)
    }

    /// A prompt that cannot answer its own question simply does not appear, and the ask is not spent —
    /// the next launch tries again.
    private func resolveJobRadiusPrompt(radiusKm: Int?, employeeRead: Bool) {
        guard employeeRead, let employeeId, !employeeId.isBlank else { return }

        guard JobRadiusPrompt.shouldPresent(
            radiusKm: radiusKm,
            hasBeenAsked: settings.hasAnsweredPrompt(JobRadiusPrompt.settingsKey, userId: employeeId)
        ) else {
            settings.markPromptAnswered(JobRadiusPrompt.settingsKey, userId: employeeId)
            return
        }

        showsJobRadiusPrompt = true
    }

    private static let previewLimit = 5
    private static let upcomingLimit = 10
}
