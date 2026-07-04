import CleansiaCore
import Combine
import Foundation

@MainActor
final class ProfileViewModel: ViewModel {
    @Published private(set) var refreshState: ActionState = .idle
    @Published private(set) var saveState: ActionState = .idle

    let saved = PassthroughSubject<Void, Never>()

    let repository: UserProfileRepository
    private let settings: AppSettingsStore
    private let snackbar: SnackbarController
    private let localizer = ApiErrorLocalizer()

    init(repository: UserProfileRepository, settings: AppSettingsStore, snackbar: SnackbarController) {
        self.repository = repository
        self.settings = settings
        self.snackbar = snackbar
    }

    var currentUser: CurrentUserProfile? {
        repository.currentUser
    }

    func refresh() async {
        guard !refreshState.isSubmitting else { return }
        refreshState = .submitting
        _ = await repository.refresh()
        refreshState = .idle
    }

    func save(
        firstName: String,
        lastName: String,
        phoneNumber: String?,
        birthDate: Date?,
        languageCode: String?
    ) async {
        guard !saveState.isSubmitting else { return }
        guard let user = repository.currentUser else {
            let message = localizer.message(for: ApiError())
            snackbar.showError(message)
            saveState = .error(message)
            return
        }
        saveState = .submitting
        let update = ProfileUpdate(
            id: user.id,
            firstName: firstName.trimmed,
            lastName: lastName.trimmed,
            phoneNumber: phoneNumber?.trimmed.nilIfEmpty,
            birthDate: birthDate,
            languageCode: languageCode
        )
        switch await repository.update(update) {
        case .success:
            saveState = .idle
            saved.send()
        case let .failure(error):
            let message = localizer.message(for: error)
            snackbar.showError(message)
            saveState = .error(message)
        }
    }

    /// Saves the onboarding fields without disturbing first/last name (the
    /// registration form already collected those). The language rides the
    /// resolved app tag — always ∈ {en,cs,sk,uk,ru} — the Android device-locale
    /// clamp (`ProfileViewModel.kt:105-106`) through the one settings store.
    func completeOnboarding(phoneNumber: String, birthDate: Date?) async {
        guard let user = repository.currentUser else { return }
        guard !saveState.isSubmitting else { return }
        saveState = .submitting
        let update = ProfileUpdate(
            id: user.id,
            firstName: user.firstName,
            lastName: user.lastName,
            phoneNumber: phoneNumber.trimmed.nilIfEmpty,
            birthDate: birthDate,
            languageCode: settings.languageTag
        )
        switch await repository.update(update) {
        case .success:
            saveState = .idle
            settings.markOnboardingSeen(userId: user.id)
            saved.send()
        case let .failure(error):
            let message = localizer.message(for: error)
            snackbar.showError(message)
            saveState = .error(message)
        }
    }

    func skipOnboarding() {
        guard let user = repository.currentUser else { return }
        settings.markOnboardingSeen(userId: user.id)
    }

    /// The post-signin onboarding gate (`MainShell.kt:157-181` parity): force a
    /// server round-trip so the decision never trusts a stale cached snapshot,
    /// then prompt once per user for an incomplete profile.
    func needsOnboarding() async -> Bool {
        await refresh()
        guard let user = repository.currentUser else { return false }
        return !user.isComplete && !settings.hasSeenOnboarding(userId: user.id)
    }
}

private extension String {
    var trimmed: String {
        trimmingCharacters(in: .whitespacesAndNewlines)
    }

    var nilIfEmpty: String? {
        isEmpty ? nil : self
    }
}
