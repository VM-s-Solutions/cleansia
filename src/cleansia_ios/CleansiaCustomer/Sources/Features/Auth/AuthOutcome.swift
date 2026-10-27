enum AuthOutcome: Equatable {
    case signedIn
    /// `password` is the one just typed on the sign-up, which the confirmation sets; nil on every other way to
    /// an unconfirmed address, where the confirm screen asks for it.
    case needsEmailConfirm(email: String, password: String? = nil)
    case passwordReset
}
