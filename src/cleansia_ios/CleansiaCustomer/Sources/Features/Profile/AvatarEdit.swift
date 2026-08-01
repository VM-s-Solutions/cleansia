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
}

/// What an avatar surface draws: the freshly picked bitmap, the stored photo, or the initials.
enum AvatarDisplay: Equatable {
    case initials
    case remote(ProfilePhoto)
    case picked(UIImage)

    static func resolve(photo: ProfilePhoto?, edit: AvatarEdit) -> AvatarDisplay {
        switch edit {
        case .unchanged:
            photo.map(AvatarDisplay.remote) ?? .initials
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
