import Foundation

public protocol AuthRefreshing: Sendable {
    func refresh(refreshToken: String) async -> RefreshedTokens?
}

public enum RefreshOutcome: Equatable, Sendable {
    case refreshed(AuthTokens)
    case signedOut
}

public actor SessionRefresher {
    private let tokenStore: TokenStore
    private let refreshClient: AuthRefreshing
    private let sessionManager: SessionManager
    private let sessionScopedCaches: SessionScopedCacheRegistry

    private var inFlight: Task<RefreshOutcome, Never>?

    public init(
        tokenStore: TokenStore,
        refreshClient: AuthRefreshing,
        sessionManager: SessionManager,
        sessionScopedCaches: SessionScopedCacheRegistry
    ) {
        self.tokenStore = tokenStore
        self.refreshClient = refreshClient
        self.sessionManager = sessionManager
        self.sessionScopedCaches = sessionScopedCaches
    }

    public func refresh(triggeredBy staleAccessToken: String?) async -> RefreshOutcome {
        guard let tokens = tokenStore.current() else {
            return .signedOut
        }

        if let staleAccessToken, staleAccessToken != tokens.accessToken {
            return .refreshed(tokens)
        }

        if let inFlight {
            return await inFlight.value
        }

        let task = Task<RefreshOutcome, Never> { [tokens] in
            await self.performRefresh(current: tokens)
        }
        inFlight = task
        let outcome = await task.value
        inFlight = nil
        return outcome
    }

    private func performRefresh(current tokens: AuthTokens) async -> RefreshOutcome {
        if tokens.isRefreshExpired() {
            await forceSignOut(.sessionExpired)
            return .signedOut
        }

        guard let refreshed = await refreshClient.refresh(refreshToken: tokens.refreshToken) else {
            await forceSignOut(.sessionExpired)
            return .signedOut
        }

        let next = AuthTokens(
            accessToken: refreshed.accessToken,
            accessTokenExpiresAt: refreshed.accessTokenExpiresAt,
            refreshToken: refreshed.refreshToken,
            refreshTokenExpiresAt: refreshed.refreshTokenExpiresAt
        )
        tokenStore.save(next)
        return .refreshed(next)
    }

    private func forceSignOut(_ reason: ForcedSignOutReason) async {
        await sessionScopedCaches.clearAll()
        tokenStore.clear()
        await sessionManager.emitForcedSignOut(reason)
    }
}
