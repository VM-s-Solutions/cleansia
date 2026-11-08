#if canImport(UIKit)
    import UIKit

    /// What the pending profile save says about the avatar. A save that says nothing must not touch the
    /// stored photo, so "no opinion" is a case of its own rather than the absence of one.
    public enum AvatarEdit: Equatable {
        case unchanged
        case removed
        case picked(image: UIImage, upload: ProfilePhotoUpload)

        /// The edit a picked photo stages, or nil for an image that cannot be encoded. The encode runs off the main
        /// actor because a camera photo takes long enough to downscale and compress to freeze the screen.
        public static func pick(_ image: UIImage) async -> AvatarEdit? {
            let encoded = await Task.detached(priority: .userInitiated) {
                ImageCompressor.encode(image)
            }.value
            guard let encoded else { return nil }
            return .picked(
                image: image,
                upload: ProfilePhotoUpload(
                    base64: encoded.base64,
                    contentType: encoded.contentType,
                    fileName: encoded.fileName
                )
            )
        }

        public var upload: ProfilePhotoUpload? {
            guard case let .picked(_, upload) = self else { return nil }
            return upload
        }

        public var isRemoval: Bool {
            self == .removed
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
