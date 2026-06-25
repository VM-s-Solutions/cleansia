import Foundation

public protocol AuthClient: AnyObject {
    func signOutLocal() async
    func logout() async
}

public protocol RefreshClient: AnyObject, AuthRefreshing {}

public protocol AuthSpine: AuthClient, RefreshClient {
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
