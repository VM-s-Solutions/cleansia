import Foundation

public enum PasswordPolicy {
    public static let minLength = 8

    public static func hasMinLength(_ password: String) -> Bool {
        password.count >= minLength
    }

    public static func hasLetter(_ password: String) -> Bool {
        password.contains(where: \.isLetter)
    }

    public static func hasNumber(_ password: String) -> Bool {
        password.contains(where: \.isNumber)
    }

    public static func isValid(_ password: String) -> Bool {
        hasMinLength(password) && hasLetter(password) && hasNumber(password)
    }

    public static func passwordsMatch(_ password: String, _ confirmation: String) -> Bool {
        !password.isEmpty && password == confirmation
    }
}
