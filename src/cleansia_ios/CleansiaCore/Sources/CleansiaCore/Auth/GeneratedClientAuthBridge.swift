import Foundation

public final class GeneratedClientAuthBridge: @unchecked Sendable {
    private let headerAdapter: HeaderAdapter
    private let tokenStore: TokenStore
    private let sessionRefresher: SessionRefresher

    public let session: URLSession

    public init(
        headerAdapter: HeaderAdapter,
        tokenStore: TokenStore,
        sessionRefresher: SessionRefresher,
        session: URLSession = .shared
    ) {
        self.headerAdapter = headerAdapter
        self.tokenStore = tokenStore
        self.sessionRefresher = sessionRefresher
        self.session = session
    }

    public func authorize(_ request: inout URLRequest) {
        headerAdapter.apply(to: &request, accessToken: tokenStore.current()?.accessToken)
    }

    public func currentAccessToken() -> String? {
        tokenStore.current()?.accessToken
    }

    public func refreshAfterUnauthorized(staleAccessToken: String?) async -> String? {
        switch await sessionRefresher.refresh(triggeredBy: staleAccessToken) {
        case let .refreshed(tokens):
            tokens.accessToken
        case .unavailable, .signedOut:
            nil
        }
    }

    public func executeWithRetry<R>(
        attempt: () async throws -> R,
        unauthorizedStatus: (Error) -> Int?
    ) async throws -> R {
        let stale = currentAccessToken()
        do {
            return try await attempt()
        } catch {
            guard unauthorizedStatus(error) == 401,
                  await refreshAfterUnauthorized(staleAccessToken: stale) != nil
            else {
                throw error
            }
            return try await attempt()
        }
    }
}
