import CleansiaCore
import CleansiaPartnerApi
import Foundation

@MainActor
final class DashboardViewModel: ViewModel {
    @Published private(set) var state: UiState<DashboardData> = .loading
    @Published private(set) var showsJobRadiusPrompt = false

    private let client: PartnerDashboardClient
    private let settings: AppSettingsStore
    private var employeeId: String?

    init(client: PartnerDashboardClient, settings: AppSettingsStore) {
        self.client = client
        self.settings = settings
    }

    func load() async {
        state = .loading

        let profile = try? await client.getCurrentEmployeeProfile().get()
        employeeId = profile?.employee.id

        switch await client.getStats(employeeId: employeeId) {
        case let .success(stats):
            let preview = try? await client.getAvailableJobsPreview(limit: Self.previewLimit).get()
            state = .loaded(DashboardData.from(
                stats: stats,
                preview: preview,
                firstName: profile?.employee.firstName
            ))
        case let .failure(error):
            state = .error(error)
        }

        resolveJobRadiusPrompt(radiusKm: profile?.jobRadiusKm, employeeRead: profile != nil)
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
}
