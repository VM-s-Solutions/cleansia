import CleansiaCore
import Foundation

@MainActor
final class CustomerAppContainer: AppContainer {
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

    var refreshClient: RefreshClient {
        base.refreshClient
    }

    var sessionRefresher: SessionRefresher {
        base.sessionRefresher
    }

    var apiClient: MobileApiClient {
        base.apiClient
    }

    init(
        snackbar: SnackbarController,
        apiBaseURL: URL = AppConfig.apiBaseURL
    ) {
        base = BaseAppContainer(
            apiBaseURL: apiBaseURL,
            snackbar: snackbar,
            makeAuthSpine: { seams in
                CustomerAuthSpine.make(
                    apiBaseURL: seams.apiBaseURL,
                    sessionScopedCaches: seams.sessionScopedCaches
                )
            },
            makeApiClient: { seams in CustomerMobileApiClient(baseURL: seams.apiBaseURL) }
        )
    }
}
