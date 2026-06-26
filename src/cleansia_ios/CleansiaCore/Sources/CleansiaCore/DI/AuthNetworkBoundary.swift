import Foundation

public final class AuthNetworkBoundary<RefreshSession, AuthedSession> {
    private let makeRefreshSession: () -> RefreshSession
    private let makeAuthedSession: () -> AuthedSession

    private lazy var _refreshSession: RefreshSession = makeRefreshSession()
    private lazy var _authedSession: AuthedSession = makeAuthedSession()

    public init(
        makeRefreshSession: @escaping () -> RefreshSession,
        makeAuthedSession: @escaping () -> AuthedSession
    ) {
        self.makeRefreshSession = makeRefreshSession
        self.makeAuthedSession = makeAuthedSession
    }

    public var refreshSession: RefreshSession {
        _refreshSession
    }

    public var authedSession: AuthedSession {
        _authedSession
    }
}
