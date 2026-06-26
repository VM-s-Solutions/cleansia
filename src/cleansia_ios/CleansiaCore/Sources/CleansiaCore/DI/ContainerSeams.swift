import Foundation

public protocol AuthClient: AnyObject {
    func signOutLocal() async
    func logout() async
}

public protocol LoginClient: AnyObject {
    func login(email: String, password: String, rememberMe: Bool) async -> ApiResult<LoginOutcome>
}

public protocol RegistrationAuthClient: AnyObject {
    func register(
        email: String,
        password: String,
        firstName: String,
        lastName: String,
        language: String
    ) async -> ApiResult<Bool>
}

public protocol EmailConfirmationClient: AnyObject {
    func confirmEmail(code: String) async -> ApiResult<LoginOutcome>
    func resendConfirmation(email: String, language: String) async -> ApiResult<Bool>
}

public protocol PasswordResetClient: AnyObject {
    func forgotPassword(email: String, language: String) async -> ApiResult<Void>
}

public protocol RefreshClient: AnyObject, AuthRefreshing {}

public typealias AuthApiClients = AuthClient & EmailConfirmationClient & LoginClient
    & PasswordResetClient & RegistrationAuthClient

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
