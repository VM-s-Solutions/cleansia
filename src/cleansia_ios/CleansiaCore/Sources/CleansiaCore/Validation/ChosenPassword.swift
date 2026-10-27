import Foundation

/// A new password typed twice and held to `PasswordPolicy`: what the confirm-email screens ask for when the
/// confirmation, which sets the password it carries, is not right after a registration.
public struct ChosenPassword: Equatable, Sendable {
    public private(set) var password = ""
    public private(set) var confirmation = ""
    public private(set) var passwordError: String?
    public private(set) var confirmationError: String?

    public init() {}

    public var hasMinLength: Bool {
        PasswordPolicy.hasMinLength(password)
    }

    public var hasLetter: Bool {
        PasswordPolicy.hasLetter(password)
    }

    public var hasNumber: Bool {
        PasswordPolicy.hasNumber(password)
    }

    public var matches: Bool {
        PasswordPolicy.passwordsMatch(password, confirmation)
    }

    public var isValid: Bool {
        PasswordPolicy.isValid(password) && matches
    }

    public mutating func setPassword(_ value: String) {
        password = value
        passwordError = nil
    }

    public mutating func setConfirmation(_ value: String) {
        confirmation = value
        confirmationError = nil
    }

    /// The password to send, or nil with each refusing field's message set.
    public mutating func validated(rulesError: String, mismatchError: String) -> String? {
        passwordError = PasswordPolicy.isValid(password) ? nil : rulesError
        confirmationError = matches ? nil : mismatchError
        return isValid ? password : nil
    }
}
