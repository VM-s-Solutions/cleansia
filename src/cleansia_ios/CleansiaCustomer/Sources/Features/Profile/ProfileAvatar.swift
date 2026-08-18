import CleansiaCore

/// The no-photo fallback Core's `ProfileAvatar` draws for this app. It reads the customer profile's own
/// name fields, which is why it stays here while the disc itself is shared.
extension CurrentUserProfile {
    var initials: String {
        let first = firstName.first.map(String.init) ?? ""
        let last = lastName.first.map(String.init) ?? ""
        return (first + last).uppercased()
    }
}
