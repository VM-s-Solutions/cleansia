enum AuthOutcome: Equatable {
    case signedIn
    case needsEmailConfirm(email: String)
    case passwordReset
}
