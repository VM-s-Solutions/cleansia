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

    init(
        repository: UserProfileRepository,
        settings: AppSettingsStore,
        snackbar: SnackbarController
    ) {
        self.repository = repository
        self.settings = settings
        self.snackbar = snackbar
        super.init()
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

    /// Apple sign-in can hand us an account with no name at all, so onboarding
    /// has to be able to collect one — replaying the stored blanks is rejected by
    /// the `UpdateCurrentUser` validators and leaves the screen a dead end.
    var onboardingNeedsName: Bool {
        guard let user = repository.currentUser else { return false }
        return user.firstName.isBlank || user.lastName.isBlank
    }

    /// `UpdateCurrentUser` requires both names; the phone is what the booking
    /// pre-flight needs.
    func canCompleteOnboarding(firstName: String, lastName: String, phoneNumber: String) -> Bool {
        !saveState.isSubmitting && !firstName.isBlank && !lastName.isBlank && !phoneNumber.isBlank
    }

    /// The language rides the resolved app tag — always ∈ {en,cs,sk,uk,ru} — the
    /// Android device-locale clamp (`ProfileViewModel.kt:105-106`) through the one
    /// settings store.
    func completeOnboarding(
        firstName: String,
        lastName: String,
        phoneNumber: String,
        birthDate: Date?
    ) async {
        guard let user = repository.currentUser else { return }
        guard !saveState.isSubmitting else { return }
        guard !firstName.isBlank, !lastName.isBlank else {
            let message = firstName.isBlank
                ? L10n.Auth.errorFirstNameRequired
                : L10n.Auth.errorLastNameRequired
            snackbar.showError(message)
            saveState = .error(message)
            return
        }
        saveState = .submitting
        let update = ProfileUpdate(
            id: user.id,
            firstName: firstName.trimmed,
            lastName: lastName.trimmed,
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
