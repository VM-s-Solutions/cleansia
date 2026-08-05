import CleansiaCore
import Combine
import Foundation
import UIKit

@MainActor
final class ProfileViewModel: ViewModel {
    @Published private(set) var refreshState: ActionState = .idle
    @Published private(set) var saveState: ActionState = .idle
    @Published private(set) var avatarEdit: AvatarEdit = .unchanged

    let saved = PassthroughSubject<Void, Never>()

    let repository: UserProfileRepository
    private let settings: AppSettingsStore
    private let snackbar: SnackbarController
    private let localizer = ApiErrorLocalizer()
    private var avatarRetriedFor: String?

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

    var editorAvatar: AvatarDisplay {
        AvatarDisplay.resolve(photo: currentUser?.profilePhoto, edit: avatarEdit)
    }

    /// There is a pick to discard, or a stored photo not already staged for deletion. Deliberately not
    /// "an image is on screen": a stored photo whose signature came back blank draws as the initials,
    /// and gating on what is drawn would hide the only way to delete it.
    var canRemoveAvatar: Bool {
        switch avatarEdit {
        case .picked: true
        case .removed: false
        case .unchanged: hasStoredPhoto
        }
    }

    private var hasStoredPhoto: Bool {
        currentUser?.profilePhoto?.fileName != nil
    }

    /// Downscaled to the same 1920px / JPEG-0.7 bound every other upload on this platform uses, and
    /// stripped of EXIF/GPS, before it is base64'd into the save. The backend validates the decoded
    /// bytes are a real image but caps no size, so the bound is ours.
    func pickAvatar(_ image: UIImage) {
        guard let encoded = ImageCompressor.encode(image) else {
            snackbar.showError(L10n.EditProfile.photoUnreadable)
            return
        }
        avatarEdit = .picked(
            image: image,
            upload: ProfilePhotoUpload(
                base64: encoded.base64,
                contentType: encoded.contentType,
                fileName: encoded.fileName
            )
        )
    }

    /// Remove is also how a pick is undone, so it is offered for an image the server has never seen.
    /// There is nothing to delete then — asking for a removal would have the save confirm one that
    /// never happened — so the pick is simply discarded.
    func removeAvatar() {
        avatarEdit = hasStoredPhoto ? .removed : .unchanged
    }

    func discardAvatarEdit() {
        avatarEdit = .unchanged
    }

    /// An image view sees neither the 403 of an expired signature nor the 404 of a deleted blob, so
    /// re-fetch the profile for a freshly signed URL and let a second failure fall back to the
    /// initials. The guard is per blob name: a later upload mints a new one and earns its own retry.
    func avatarLoadFailed(fileName: String) async {
        guard avatarRetriedFor != fileName else { return }
        avatarRetriedFor = fileName
        await refresh()
    }

    /// The signature the refetch bought expires too, so a rendered image releases the guard: the next
    /// failure of the same blob is a fresh expiry, not the loop the guard exists to stop.
    func avatarLoadSucceeded() {
        avatarRetriedFor = nil
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
            languageCode: languageCode,
            photo: avatarEdit.upload,
            removePhoto: avatarEdit.isRemoval
        )
        let confirmation = AvatarSaveConfirmation.forEdit(avatarEdit)
        switch await repository.update(update) {
        case .success:
            saveState = .idle
            avatarEdit = .unchanged
            snackbar.showSuccess(confirmation?.message ?? L10n.Profile.saveSuccess)
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
