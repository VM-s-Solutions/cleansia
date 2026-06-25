import Foundation

@MainActor
public protocol AppContainer: AnyObject {
    var apiBaseURL: URL { get }
    var snackbar: SnackbarController { get }
    var sessionScopedCaches: SessionScopedCacheRegistry { get }
    var sessionManager: SessionManager { get }
    var authClient: AuthClient { get }
    var refreshClient: RefreshClient { get }
    var sessionRefresher: SessionRefresher { get }
}

public struct ContainerSeams {
    public let apiBaseURL: URL
    public let snackbar: SnackbarController
    public let sessionScopedCaches: SessionScopedCacheRegistry
    public let refreshClient: RefreshClient
}

public struct AuthSpineSeams {
    public let apiBaseURL: URL
    public let snackbar: SnackbarController
    public let sessionScopedCaches: SessionScopedCacheRegistry
}

@MainActor
public final class BaseAppContainer: AppContainer {
    public let apiBaseURL: URL
    public let snackbar: SnackbarController
    public let sessionScopedCaches: SessionScopedCacheRegistry
    public let sessionManager: SessionManager

    private let makeAuthSpine: (AuthSpineSeams) -> AuthSpine
    private let makeApiClient: (ContainerSeams) -> MobileApiClient

    public init(
        apiBaseURL: URL,
        snackbar: SnackbarController,
        sessionScopedCaches: SessionScopedCacheRegistry = SessionScopedCacheRegistry(),
        makeAuthSpine: @escaping (AuthSpineSeams) -> AuthSpine,
        makeApiClient: @escaping (ContainerSeams) -> MobileApiClient
    ) {
        self.apiBaseURL = apiBaseURL
        self.snackbar = snackbar
        self.sessionScopedCaches = sessionScopedCaches
        sessionManager = SessionManager(sessionScopedCaches: sessionScopedCaches)
        self.makeAuthSpine = makeAuthSpine
        self.makeApiClient = makeApiClient
    }

    public lazy var authSpine: AuthSpine = makeAuthSpine(
        AuthSpineSeams(apiBaseURL: apiBaseURL, snackbar: snackbar, sessionScopedCaches: sessionScopedCaches)
    )

    public var authClient: AuthClient { authSpine }
    public var refreshClient: RefreshClient { authSpine }

    public lazy var sessionRefresher = SessionRefresher(
        tokenStore: tokenStore,
        refreshClient: authSpine,
        sessionManager: sessionManager,
        sessionScopedCaches: sessionScopedCaches
    )

    public lazy var apiClient: MobileApiClient = makeApiClient(
        ContainerSeams(
            apiBaseURL: apiBaseURL,
            snackbar: snackbar,
            sessionScopedCaches: sessionScopedCaches,
            refreshClient: authSpine
        )
    )

    private var tokenStore: TokenStore { authSpine.tokenStore }
}
