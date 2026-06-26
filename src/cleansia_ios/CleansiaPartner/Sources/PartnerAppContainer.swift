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

    init(
        snackbar: SnackbarController,
        apiBaseURL: URL = AppConfig.apiBaseURL
    ) {
        base = BaseAppContainer(
            apiBaseURL: apiBaseURL,
            snackbar: snackbar,
            makeAuthSpine: { seams in
                PartnerAuthSpine.make(
                    apiBaseURL: seams.apiBaseURL,
                    sessionScopedCaches: seams.sessionScopedCaches
                )
            },
            makeApiClient: { seams in PartnerMobileApiClient(baseURL: seams.apiBaseURL) }
        )
    }
}
