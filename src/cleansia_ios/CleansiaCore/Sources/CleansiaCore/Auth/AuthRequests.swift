import Foundation

public struct LoginRequest: Encodable, Sendable {
    public let email: String
    public let password: String
    public let rememberMe: Bool

    public init(email: String, password: String, rememberMe: Bool = true) {
        self.email = email
        self.password = password
        self.rememberMe = rememberMe
    }
}

public struct RefreshTokenRequest: Encodable, Sendable {
    public let token: String
    public init(token: String) {
        self.token = token
    }
}

public struct LogoutRequest: Encodable, Sendable {
    public let token: String
    public init(token: String) {
        self.token = token
    }
}

public struct RegisterRequest: Encodable, Sendable {
    public let email: String
    public let password: String
    public let firstName: String
    public let lastName: String
    public let language: String
}

public struct ConfirmUserEmailRequest: Encodable, Sendable {
    public let code: String
}

public struct ResendConfirmationEmailRequest: Encodable, Sendable {
    public let email: String
    public let language: String
}

public struct ForgotPasswordRequest: Encodable, Sendable {
    public let email: String
    public let language: String
}

public struct GoogleAuthRequest: Encodable, Sendable {
    public let token: String
    public let googleId: String
    public let email: String
    public let firstName: String
    public let lastName: String
}

public struct AppleAuthRequest: Encodable, Sendable {
    public let identityToken: String
    public let rawNonce: String
    public let firstName: String?
    public let lastName: String?
}
