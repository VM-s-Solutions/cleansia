import CleansiaCore

/// The customer's wording for the two outcomes Core's `AvatarSaveConfirmation` names. Core resolves no
/// app strings, and both apps word the same two outcomes for their own audience, so the copy is the one
/// part of the confirmation that does not move.
extension AvatarSaveConfirmation {
    var message: String {
        switch self {
        case .uploaded: L10n.EditProfile.photoUploadSuccess
        case .removed: L10n.EditProfile.photoRemoveSuccess
        }
    }
}
