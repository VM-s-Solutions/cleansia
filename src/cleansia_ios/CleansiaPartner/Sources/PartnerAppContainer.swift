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

    var registrationAuthClient: RegistrationAuthClient {
        base.registrationAuthClient
    }

    var emailConfirmationClient: EmailConfirmationClient {
        base.emailConfirmationClient
    }

    var passwordResetClient: PasswordResetClient {
        base.passwordResetClient
    }

    var refreshClient: RefreshClient {
        base.refreshClient
    }

    var sessionRefresher: SessionRefresher {
        base.sessionRefresher
    }

    var appSettings: AppSettingsStore {
        base.appSettings
    }

    var hasValidSession: Bool {
        base.hasValidSession
    }

    var apiClient: MobileApiClient {
        base.apiClient
    }

    let dashboardClient: PartnerDashboardClient = LivePartnerDashboardClient()
    let registrationClient: PartnerRegistrationClient = LivePartnerRegistrationClient()
    let profileClient: PartnerProfileClient = LivePartnerProfileClient()
    let devicesClient: PartnerDevicesClient
    let orderClient: PartnerOrderClient = LivePartnerOrderClient()
    let ordersStaleness = OrdersStaleness()
    let geocodingService: GeocodingService = CLGeocoderGeocodingService()
    let mapProvider: MapProvider = MapKitMapProvider()

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
        // D6: the Devices client gets the ONE device-id source — the same
        // DeviceIdProvider the HeaderAdapter stamps as X-Device-Id.
        let devicesClient = LivePartnerDevicesClient(deviceIdProvider: authStack.deviceIdProvider)
        self.devicesClient = devicesClient
        base = BaseAppContainer(
            apiBaseURL: apiBaseURL,
            snackbar: snackbar,
            sessionScopedCaches: sessionScopedCaches,
            makeAuthSpine: { _ in authStack.spine },
            makeApiClient: { seams in PartnerMobileApiClient(baseURL: seams.apiBaseURL) }
        )
        if let cache = profileClient as? SessionScopedCache {
            sessionScopedCaches.register(cache)
        }
        if let cache = devicesClient as? SessionScopedCache {
            sessionScopedCaches.register(cache)
        }
        sessionScopedCaches.register(ordersStaleness)
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
