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
            languageCode: nil
        )
        switch await repository.update(update) {
        case .success:
            saveState = .idle
            settings.markOnboardingSeen()
            saved.send()
        case let .failure(error):
            let message = localizer.message(for: error)
            snackbar.showError(message)
            saveState = .error(message)
        }
    }

    func skipOnboarding() {
        settings.markOnboardingSeen()
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
