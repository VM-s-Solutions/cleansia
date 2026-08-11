import Foundation

public protocol AuthClient: AnyObject {
    func signOutLocal() async
    func logout() async
}

public protocol LoginClient: AnyObject {
    func login(email: String, password: String, rememberMe: Bool) async -> ApiResult<LoginOutcome>
}

public protocol RegistrationAuthClient: AnyObject {
    func register(_ request: RegisterRequest) async -> ApiResult<Bool>
}

public extension RegistrationAuthClient {
    func register(
        email: String,
        password: String,
        firstName: String,
        lastName: String,
        language: String,
        referralCode: String? = nil
    ) async -> ApiResult<Bool> {
        await register(RegisterRequest(
            email: email,
            password: password,
            firstName: firstName,
            lastName: lastName,
            language: language,
            referralCode: referralCode
        ))
    }
}

public protocol EmailConfirmationClient: AnyObject {
    // The email is REQUIRED with the 6-digit code: the server resolves the account by email and only
    // proves the code against it (a bare code is never a lookup key).
    func confirmEmail(email: String, code: String) async -> ApiResult<LoginOutcome>
    func resendConfirmation(email: String, language: String) async -> ApiResult<Bool>
}

public protocol PasswordResetClient: AnyObject {
    func forgotPassword(email: String, language: String) async -> ApiResult<Void>
}

/// `termsAccepted` is what tells a signup apart from a sign-in: both screens call one endpoint, and
/// the server provisions an identity it has never seen only for a call that asserts the signup
/// screen's tick — everything else is refused with `auth.social_account_not_found`. It carries no
/// default for the same reason `SignupConsentRecording.recordSignupTick` does not: a consent flag
/// that can be omitted at a call site is a consent nobody gave.
public protocol SocialAuthClient: AnyObject {
    func googleAuth(_ request: GoogleAuthRequest) async -> ApiResult<LoginOutcome>
    func appleAuth(_ request: AppleAuthRequest) async -> ApiResult<LoginOutcome>
}

public extension SocialAuthClient {
    func googleAuth(
        _ credential: SocialSignInResult.GoogleCredential,
        termsAccepted: Bool
    ) async -> ApiResult<LoginOutcome> {
        await googleAuth(GoogleAuthRequest(
            token: credential.idToken,
            googleId: credential.googleId,
            email: credential.email,
            firstName: credential.firstName,
            lastName: credential.lastName,
            termsAccepted: termsAccepted
        ))
    }

    func appleAuth(
        _ credential: SocialSignInResult.AppleCredential,
        termsAccepted: Bool
    ) async -> ApiResult<LoginOutcome> {
        await appleAuth(AppleAuthRequest(
            identityToken: credential.identityToken,
            rawNonce: credential.rawNonce,
            firstName: credential.firstName,
            lastName: credential.lastName,
            termsAccepted: termsAccepted
        ))
    }
}

public protocol RefreshClient: AnyObject, AuthRefreshing {}

public typealias AuthApiClients = AuthClient & EmailConfirmationClient & LoginClient
    & PasswordResetClient & RegistrationAuthClient & SocialAuthClient

public protocol AuthSpine: AuthApiClients, RefreshClient {
    var tokenStore: TokenStore { get }
}

public struct RefreshedTokens: Equatable, Sendable {
    public let accessToken: String
    public let accessTokenExpiresAt: Date
    public let refreshToken: String
    public let refreshTokenExpiresAt: Date

    public init(
        accessToken: String,
        accessTokenExpiresAt: Date,
        refreshToken: String,
        refreshTokenExpiresAt: Date
    ) {
        self.accessToken = accessToken
        self.accessTokenExpiresAt = accessTokenExpiresAt
        self.refreshToken = refreshToken
        self.refreshTokenExpiresAt = refreshTokenExpiresAt
    }
}

public protocol MobileApiClient: AnyObject {
    var baseURL: URL { get }
}
