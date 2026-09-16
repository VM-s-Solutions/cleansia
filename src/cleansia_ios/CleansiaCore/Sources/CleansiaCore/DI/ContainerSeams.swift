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
    // One label per wire member; `countryId` and `termsAccepted` deliberately carry no default so
    // every caller names the market and the tick (or their absence) rather than forgetting them.
    // swiftlint:disable:next function_parameter_count
    func register(
        email: String,
        password: String,
        firstName: String,
        lastName: String,
        language: String,
        referralCode: String? = nil,
        countryId: String?,
        termsAccepted: Bool?
    ) async -> ApiResult<Bool> {
        await register(RegisterRequest(
            email: email,
            password: password,
            firstName: firstName,
            lastName: lastName,
            language: language,
            referralCode: referralCode,
            countryId: countryId,
            termsAccepted: termsAccepted
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
/// that can be omitted at a call site is a consent nobody gave. `countryId` carries no default for
/// the neighbouring reason: a market that can be omitted is a signup that silently lands with the
/// default operating company, and the call site is the only place that knows whether that is so.
public protocol SocialAuthClient: AnyObject {
    func googleAuth(_ request: GoogleAuthRequest) async -> ApiResult<LoginOutcome>
    func appleAuth(_ request: AppleAuthRequest) async -> ApiResult<LoginOutcome>
}

public extension SocialAuthClient {
    func googleAuth(
        _ credential: SocialSignInResult.GoogleCredential,
        termsAccepted: Bool,
        countryId: String?
    ) async -> ApiResult<LoginOutcome> {
        await googleAuth(GoogleAuthRequest(
            token: credential.idToken,
            googleId: credential.googleId,
            email: credential.email,
            firstName: credential.firstName,
            lastName: credential.lastName,
            termsAccepted: termsAccepted,
            countryId: countryId
        ))
    }

    func appleAuth(
        _ credential: SocialSignInResult.AppleCredential,
        termsAccepted: Bool,
        countryId: String?
    ) async -> ApiResult<LoginOutcome> {
        await appleAuth(AppleAuthRequest(
            identityToken: credential.identityToken,
            rawNonce: credential.rawNonce,
            firstName: credential.firstName,
            lastName: credential.lastName,
            termsAccepted: termsAccepted,
            countryId: countryId
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

/// A secret-keyed guest call rides the no-auth session on purpose: the server verifies the confirmation
/// code, never a Bearer, and a stale account token beside the key would let an expired session answer
/// for a booking that account never owned.
public protocol AnonymousPosting: AnyObject, Sendable {
    func postAnonymous<Response: Decodable>(path: String, body: some Encodable) async -> ApiResult<Response>
}
