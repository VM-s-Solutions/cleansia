import Foundation

/// One refresh-endpoint call, classified per the cross-platform session contract
/// (identical on Android). `.rejected` is terminal: the endpoint answered with an
/// auth rejection — HTTP 401/403 or a parseable invalid/expired/revoked
/// refresh-token business error — so the session is dead. `.retryable` is every
/// other failure (transport: unreachable/timeout/DNS/TLS; HTTP 5xx; HTTP 429; any
/// unknown non-auth answer): fail-open for the session — tokens are kept, the
/// caller surfaces its own failure, and the next trigger retries. The server
/// still rejects a genuinely bad token on that next call.
public enum RefreshCallResult: Equatable, Sendable {
    case refreshed(RefreshedTokens)
    case rejected
    case retryable
}

public extension RefreshCallResult {
    static func classify(_ error: ApiError) -> RefreshCallResult {
        if error.httpStatus == 401 || error.httpStatus == 403 {
            return .rejected
        }
        // Parity with Android's whole-body substring scan
        // (AuthAuthenticator.classifyHttpFailure): a rejection can ride a
        // non-401 status with the business key in the ProblemDetails code/type
        // (→ `code`, via decodeError's `?? problem.type`) or the free-text body
        // (→ `message`). Match either so a real rejection still ends the session.
        if rejectionCodes.contains(where: { key in
            error.code == key || (error.message?.contains(key) ?? false)
        }) {
            return .rejected
        }
        return .retryable
    }

    static let rejectionCodes: Set<String> = [
        "auth.invalid_refresh_token",
        "auth.refresh_token_reused"
    ]
}

public protocol AuthRefreshing: Sendable {
    func refresh(refreshToken: String) async -> RefreshCallResult
}

public enum RefreshOutcome: Equatable, Sendable {
    case refreshed(AuthTokens)
    /// The refresh could not complete but the session survives: tokens kept, no
    /// forced sign-out, and the next trigger runs a fresh attempt.
    case unavailable
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
        // Empty stored refresh token is locally-expired-terminal: Auth.persist
        // can store `refreshToken: ""` when a login/refresh response omits it,
        // and posting "" to the endpoint would only draw a rejection round-trip.
        // Treat it as a dead session, exactly like an expired one.
        if tokens.refreshToken.isEmpty || tokens.isRefreshExpired() {
            await forceSignOut(.sessionExpired)
            return .signedOut
        }

        switch await refreshClient.refresh(refreshToken: tokens.refreshToken) {
        case .rejected:
            await forceSignOut(.sessionExpired)
            return .signedOut
        case .retryable:
            return .unavailable
        case let .refreshed(refreshed):
            let next = AuthTokens(
                accessToken: refreshed.accessToken,
                accessTokenExpiresAt: refreshed.accessTokenExpiresAt,
                refreshToken: refreshed.refreshToken,
                refreshTokenExpiresAt: refreshed.refreshTokenExpiresAt
            )
            tokenStore.save(next)
            return .refreshed(next)
        }
    }

    private func forceSignOut(_ reason: ForcedSignOutReason) async {
        await sessionScopedCaches.clearAll()
        tokenStore.clear()
        await sessionManager.emitForcedSignOut(reason)
    }
}
