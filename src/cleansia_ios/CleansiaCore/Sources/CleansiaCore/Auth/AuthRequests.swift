import Foundation

public struct LoginRequest: Encodable, Sendable {
    public let email: String
    public let password: String
    public let rememberMe: Bool
    public let trustedDeviceToken: String?

    public init(
        email: String,
        password: String,
        rememberMe: Bool = true,
        trustedDeviceToken: String? = nil
    ) {
        self.email = email
        self.password = password
        self.rememberMe = rememberMe
        self.trustedDeviceToken = trustedDeviceToken
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
    public let referralCode: String?
    /// The market the visitor chose; absent, the server registers them with the default market's
    /// operating company. Every market-scoped anonymous request carries the same optional member.
    public let countryId: String?
    /// The sign-up screen's terms tick, granted server-side in the registration's own commit. Absent
    /// is "not asserted" — the only value a form without the box can honestly send.
    public let termsAccepted: Bool?
}

/// The email names the account the 6-digit code was issued to — the server verifies the code ONLY
/// against that account (a bare code proves nothing by itself).
public struct ConfirmUserEmailRequest: Encodable, Sendable {
    public let code: String
    public let email: String
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
    public let termsAccepted: Bool
    public let countryId: String?
}

public struct AppleAuthRequest: Encodable, Sendable {
    public let identityToken: String
    public let rawNonce: String
    public let firstName: String?
    public let lastName: String?
    public let termsAccepted: Bool
    public let countryId: String?
}
