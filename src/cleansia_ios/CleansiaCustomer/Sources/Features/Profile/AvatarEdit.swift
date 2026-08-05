import UIKit

/// What the pending profile save says about the avatar. A save that says nothing must not touch the
/// stored photo, so "no opinion" is a case of its own rather than the absence of one.
enum AvatarEdit: Equatable {
    case unchanged
    case removed
    case picked(image: UIImage, upload: ProfilePhotoUpload)

    var upload: ProfilePhotoUpload? {
        guard case let .picked(_, upload) = self else { return nil }
        return upload
    }

    var isRemoval: Bool {
        self == .removed
    }

    var isPick: Bool {
        upload != nil
    }
}

/// What to confirm once the server has accepted a save carrying this edit. Nil for `.unchanged`:
/// that save touched no photo, so a photo message would claim something that never happened.
enum AvatarSaveConfirmation: Equatable {
    case uploaded
    case removed

    static func forEdit(_ edit: AvatarEdit) -> AvatarSaveConfirmation? {
        switch edit {
        case .unchanged: nil
        case .removed: .removed
        case .picked: .uploaded
        }
    }

    var message: String {
        switch self {
        case .uploaded: L10n.EditProfile.photoUploadSuccess
        case .removed: L10n.EditProfile.photoRemoveSuccess
        }
    }
}

/// What an avatar surface draws: the freshly picked bitmap, the stored photo, or the initials.
enum AvatarDisplay: Equatable {
    case initials
    case remote(ProfilePhoto)
    case picked(UIImage)

    /// A stored photo with no signature this fetch draws as the initials — there is nothing to load —
    /// which is a rendering fact and not a statement about whether the account holds a photo.
    static func resolve(photo: ProfilePhoto?, edit: AvatarEdit) -> AvatarDisplay {
        switch edit {
        case .unchanged:
            photo.flatMap { $0.blobURL == nil ? nil : AvatarDisplay.remote($0) } ?? .initials
        case .removed:
            .initials
        case let .picked(image, _):
            .picked(image)
        }
    }

    var isImage: Bool {
        self != .initials
    }
}
