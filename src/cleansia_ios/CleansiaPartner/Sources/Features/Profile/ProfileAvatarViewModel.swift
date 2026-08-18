import CleansiaCore
import Foundation
import UIKit

/// The cleaner's profile photo, read and written through the USER row rather than the employee record
/// the rest of the hub is built from — `EmployeeItem.profilePhoto` is never signed on this host, so
/// `MyProfileDto` is the only place a stored avatar exists to draw.
///
/// Deliberately its own model rather than another field on `ProfileViewModel`: the hub's data is one
/// employee fetch that fails as a unit, and a photo the user can pick, discard and re-pick between
/// saves is not part of that. Nothing here blocks the hub — a failed read just leaves the initials.
@MainActor
final class ProfileAvatarViewModel: ViewModel {
    @Published private(set) var user: CurrentUser?
    @Published private(set) var edit: AvatarEdit = .unchanged
    @Published private(set) var action: ActionState = .idle

    private let client: PartnerUserClient
    private let snackbar: SnackbarController
    private let localizer = ApiErrorLocalizer()
    private var retriedFor: String?

    init(client: PartnerUserClient, snackbar: SnackbarController) {
        self.client = client
        self.snackbar = snackbar
    }

    var display: AvatarDisplay {
        AvatarDisplay.resolve(photo: user?.profilePhoto, edit: edit)
    }

    var hasPendingEdit: Bool {
        edit != .unchanged
    }

    /// There is a pick to discard, or a stored photo not already staged for deletion. Deliberately not
    /// "an image is on screen": a stored photo whose signature came back blank draws as the initials,
    /// and gating on what is drawn would hide the only way to delete it.
    var canRemove: Bool {
        switch edit {
        case .picked: true
        case .removed: false
        case .unchanged: hasStoredPhoto
        }
    }

    private var hasStoredPhoto: Bool {
        user?.profilePhoto?.fileName != nil
    }

    func load() async {
        if case let .success(user) = await client.getCurrentUser() {
            self.user = user
        }
    }

    /// Downscaled to the same 1920px / JPEG-0.7 bound every other upload on this platform uses, and
    /// stripped of EXIF/GPS, before it is base64'd into the save. The backend validates the decoded
    /// bytes are a real image but caps no size, so the bound is ours.
    func pick(_ image: UIImage) {
        guard let encoded = ImageCompressor.encode(image) else {
            snackbar.showError(L10n.Profile.photoUnreadable)
            return
        }
        edit = .picked(
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
    func remove() {
        edit = hasStoredPhoto ? .removed : .unchanged
    }

    func discard() {
        edit = .unchanged
    }

    /// The pick is staged until this runs: nothing reaches the server on the tap that chose the image,
    /// so a cleaner who picked the wrong one discards it instead of uploading and re-uploading.
    ///
    /// `UpdateCurrentUser` replaces first and last name outright — the same reason the language push
    /// replays them — so the whole user row travels beside the photo. A row missing a name would be
    /// blanked by the save rather than rejected by it, so it is refused here instead.
    func save() async {
        guard hasPendingEdit, !action.isSubmitting, let user else { return }
        guard let firstName = user.firstName?.trimmedOrNil else {
            snackbar.showError(L10n.Profile.errorFirstNameRequired)
            return
        }
        guard let lastName = user.lastName?.trimmedOrNil else {
            snackbar.showError(L10n.Profile.errorLastNameRequired)
            return
        }
        let confirmation = AvatarSaveConfirmation.forEdit(edit)
        action = .submitting
        let result = await client.updateCurrentUser(CurrentUserUpdate(
            firstName: firstName,
            lastName: lastName,
            phoneNumber: user.phoneNumber ?? "",
            birthDate: user.birthDate,
            languageCode: user.preferredLanguageCode,
            photo: edit.upload,
            removePhoto: edit.isRemoval
        ))
        switch result {
        case .success:
            action = .idle
            // Re-read BEFORE the staged edit is cleared. The response says nothing about the blob and
            // the new name is what the cache keys on, so only a re-read knows what was stored — and
            // `display` falls back to the photo this save replaced the moment the edit stops speaking.
            await load()
            edit = .unchanged
            if let confirmation {
                snackbar.showSuccess(message(for: confirmation))
            }
        case let .failure(error):
            let message = localizer.message(for: error)
            action = .error(message)
            snackbar.showError(message)
        }
    }

    /// An image view sees neither the 403 of an expired signature nor the 404 of a deleted blob, so
    /// re-fetch the profile for a freshly signed URL and let a second failure fall back to the
    /// initials. The guard is per blob name: a later upload mints a new one and earns its own retry.
    func loadFailed(fileName: String) async {
        guard retriedFor != fileName else { return }
        retriedFor = fileName
        await load()
    }

    /// The signature the refetch bought expires too, so a rendered image releases the guard: the next
    /// failure of the same blob is a fresh expiry, not the loop the guard exists to stop.
    func loadSucceeded() {
        retriedFor = nil
    }

    private func message(for confirmation: AvatarSaveConfirmation) -> String {
        switch confirmation {
        case .uploaded: L10n.Profile.photoUploadSuccess
        case .removed: L10n.Profile.photoRemoveSuccess
        }
    }
}
