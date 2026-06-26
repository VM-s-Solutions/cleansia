import CleansiaCore
import Foundation

@MainActor
final class PartnerAppContainer: AppContainer {
    private let base: BaseAppContainer

    var apiBaseURL: URL {
        base.apiBaseURL
    }

    var snackbar: SnackbarController {
        base.snackbar
    }

    var sessionScopedCaches: SessionScopedCacheRegistry {
        base.sessionScopedCaches
    }

    var sessionManager: SessionManager {
        base.sessionManager
    }

    var authClient: AuthClient {
        base.authClient
    }

    var loginClient: LoginClient {
        base.loginClient
    }

    var refreshClient: RefreshClient {
        base.refreshClient
    }

    var sessionRefresher: SessionRefresher {
        base.sessionRefresher
    }

    var hasValidSession: Bool {
        base.hasValidSession
    }

    var apiClient: MobileApiClient {
        base.apiClient
    }

    let dashboardClient: PartnerDashboardClient = LivePartnerDashboardClient()
    let registrationClient: PartnerRegistrationClient = LivePartnerRegistrationClient()

    private let authStack: PartnerAuthStack

    init(
        snackbar: SnackbarController,
        apiBaseURL: URL = AppConfig.apiBaseURL
    ) {
        let sessionScopedCaches = SessionScopedCacheRegistry()
        let authStack = PartnerAuthSpine.make(
            apiBaseURL: apiBaseURL,
            sessionScopedCaches: sessionScopedCaches
        )
        self.authStack = authStack
        base = BaseAppContainer(
            apiBaseURL: apiBaseURL,
            snackbar: snackbar,
            sessionScopedCaches: sessionScopedCaches,
            makeAuthSpine: { _ in authStack.spine },
            makeApiClient: { seams in PartnerMobileApiClient(baseURL: seams.apiBaseURL) }
        )
    }

    func installGeneratedClientAuth() {
        let bridge = GeneratedClientAuthBridge(
            headerAdapter: authStack.headerAdapter,
            tokenStore: authStack.spine.tokenStore,
            sessionRefresher: base.sessionRefresher,
            session: URLSession(configuration: .default)
        )
        PartnerGeneratedAuth.install(bridge: bridge, basePath: apiBaseURL.absoluteString)
    }
}
