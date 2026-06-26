import Foundation

@MainActor
public protocol AppContainer: AnyObject {
    var apiBaseURL: URL { get }
    var snackbar: SnackbarController { get }
    var sessionScopedCaches: SessionScopedCacheRegistry { get }
    var sessionManager: SessionManager { get }
    var authClient: AuthClient { get }
    var loginClient: LoginClient { get }
    var registrationAuthClient: RegistrationAuthClient { get }
    var emailConfirmationClient: EmailConfirmationClient { get }
    var passwordResetClient: PasswordResetClient { get }
    var refreshClient: RefreshClient { get }
    var sessionRefresher: SessionRefresher { get }
    var appSettings: AppSettingsStore { get }
    var hasValidSession: Bool { get }
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
        appSettings: AppSettingsStore = UserDefaultsAppSettingsStore(),
        makeAuthSpine: @escaping (AuthSpineSeams) -> AuthSpine,
        makeApiClient: @escaping (ContainerSeams) -> MobileApiClient
    ) {
        self.apiBaseURL = apiBaseURL
        self.snackbar = snackbar
        self.sessionScopedCaches = sessionScopedCaches
        self.appSettings = appSettings
        sessionManager = SessionManager(sessionScopedCaches: sessionScopedCaches)
        self.makeAuthSpine = makeAuthSpine
        self.makeApiClient = makeApiClient
    }

    public lazy var authSpine: AuthSpine = makeAuthSpine(
        AuthSpineSeams(apiBaseURL: apiBaseURL, snackbar: snackbar, sessionScopedCaches: sessionScopedCaches)
    )

    public var authClient: AuthClient {
        authSpine
    }

    public var loginClient: LoginClient {
        authSpine
    }

    public var registrationAuthClient: RegistrationAuthClient {
        authSpine
    }

    public var emailConfirmationClient: EmailConfirmationClient {
        authSpine
    }

    public var passwordResetClient: PasswordResetClient {
        authSpine
    }

    public var refreshClient: RefreshClient {
        authSpine
    }

    public let appSettings: AppSettingsStore

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

    public var hasValidSession: Bool {
        guard let accessToken = tokenStore.current()?.accessToken else { return false }
        return !accessToken.isEmpty
    }

    private var tokenStore: TokenStore {
        authSpine.tokenStore
    }
}
