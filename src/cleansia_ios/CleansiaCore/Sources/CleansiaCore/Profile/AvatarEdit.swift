#if canImport(UIKit)
    import UIKit

    /// What the pending profile save says about the avatar. A save that says nothing must not touch the
    /// stored photo, so "no opinion" is a case of its own rather than the absence of one.
    public enum AvatarEdit: Equatable {
        case unchanged
        case removed
        case picked(image: UIImage, upload: ProfilePhotoUpload)

        public var upload: ProfilePhotoUpload? {
            guard case let .picked(_, upload) = self else { return nil }
            return upload
        }

        public var isRemoval: Bool {
            self == .removed
        }

        public var isPick: Bool {
            upload != nil
        }
    }

    /// What to confirm once the server has accepted a save carrying this edit. Nil for `.unchanged`:
    /// that save touched no photo, so a photo message would claim something that never happened.
    ///
    /// The copy stays with each app: both catalogues word the two outcomes for their own audience, and
    /// Core owns no strings either of them reads.
    public enum AvatarSaveConfirmation: Equatable {
        case uploaded
        case removed

        public static func forEdit(_ edit: AvatarEdit) -> AvatarSaveConfirmation? {
            switch edit {
            case .unchanged: nil
            case .removed: .removed
            case .picked: .uploaded
            }
        }
    }

    /// What an avatar surface draws: the freshly picked bitmap, the stored photo, or the initials.
    public enum AvatarDisplay: Equatable {
        case initials
        case remote(ProfilePhoto)
        case picked(UIImage)

        /// A stored photo with no signature this fetch draws as the initials — there is nothing to load —
        /// which is a rendering fact and not a statement about whether the account holds a photo.
        public static func resolve(photo: ProfilePhoto?, edit: AvatarEdit) -> AvatarDisplay {
            switch edit {
            case .unchanged:
                photo.flatMap { $0.blobURL == nil ? nil : AvatarDisplay.remote($0) } ?? .initials
            case .removed:
                .initials
            case let .picked(image, _):
                .picked(image)
            }
        }

        public var isImage: Bool {
            self != .initials
        }
    }
#endif
