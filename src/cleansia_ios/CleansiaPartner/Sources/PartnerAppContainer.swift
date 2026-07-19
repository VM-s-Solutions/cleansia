import CleansiaCore
import CleansiaPartnerApi
import Combine
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
    let payrollClient: PartnerPayrollClient = LivePartnerPayrollClient()
    let registrationClient: PartnerRegistrationClient = LivePartnerRegistrationClient()
    let profileClient: PartnerProfileClient = LivePartnerProfileClient()
    let devicesClient: PartnerDevicesClient
    let orderClient: PartnerOrderClient = LivePartnerOrderClient()
    let ordersStaleness = OrdersStaleness()
    let invoicesStaleness = InvoicesStaleness()
    let cleaningChecklistStore: CleaningChecklistStore = UserDefaultsCleaningChecklistStore()
    let geocodingService: GeocodingService = CLGeocoderGeocodingService()
    let mapProvider: MapProvider = MapKitMapProvider()
    lazy var serviceArea = ServiceAreaProvider(dataSource: PartnerServiceAreaDataSource(client: profileClient))
    let pushRegistrar: any PushRegistrar = UNUserNotificationPushRegistrar()
    let notificationFeedClient: NotificationFeedClient
    let notificationBadge: NotificationBadgeModel

    private let authStack: PartnerAuthStack
    private let pushTokenRegistrar: PushTokenRegistrar
    private let pushSessionObserver: PushSessionObserver
    private let hasSessionSubject: CurrentValueSubject<Bool, Never>

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
        let notificationFeedClient = LiveNotificationFeedClient()
        self.notificationFeedClient = notificationFeedClient
        notificationBadge = NotificationBadgeModel(client: notificationFeedClient)
        let pushTokenRegistrar = PushTokenRegistrar(
            client: PartnerDeviceRegistrationClient(),
            deviceIdProvider: authStack.deviceIdProvider
        )
        self.pushTokenRegistrar = pushTokenRegistrar
        pushSessionObserver = PushSessionObserver(registrar: pushTokenRegistrar)
        hasSessionSubject = CurrentValueSubject(false)
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
        sessionScopedCaches.register(invoicesStaleness)
        sessionScopedCaches.register(notificationBadge)
        sessionScopedCaches.register(pushTokenRegistrar)
        // Rule 3: the authed Device/Unregister DELETE runs while the Bearer is
        // still live, before logout() wipes the token. The local cache clear()
        // rides the SessionScopedCacheRegistry on every sign-out (above).
        authStack.spine.setPreLogout { [pushTokenRegistrar] in
            await pushTokenRegistrar.unregisterDevice()
        }
    }

    /// Starts APNs registration + the session×token observer. Called once from
    /// the App after auth is installed. The session signal is seeded from the
    /// current token state (cold-start-into-authed-session parity) and pushed
    /// forward by `updatePushSession(hasSession:)` at each session transition.
    func startPush() {
        hasSessionSubject.send(hasValidSession)
        pushSessionObserver.attach(
            hasSession: hasSessionSubject.eraseToAnyPublisher(),
            apnsToken: pushRegistrar.apnsToken
        )
        // Alert-display permission only — APNs registration itself happens in
        // the app delegate's didFinishLaunching (deferring it there gets
        // silently dropped by iOS).
        Task { _ = await pushRegistrar.requestAuthorization() }
    }

    func updatePushSession(hasSession: Bool) {
        hasSessionSubject.send(hasSession)
    }

    func installGeneratedClientAuth() {
        let bridge = GeneratedClientAuthBridge(
            headerAdapter: authStack.headerAdapter,
            tokenStore: authStack.spine.tokenStore,
            sessionRefresher: base.sessionRefresher,
            session: URLSession(configuration: .default)
        )
        PartnerGeneratedAuth.install(bridge: bridge, basePath: apiBaseURL.absoluteString)
        // Response processing + Codable decode happen on this queue instead of
        // main; call sites await via continuations, so UI updates still hop back.
        CleansiaPartnerApiAPI.apiResponseQueue = DispatchQueue(label: "cz.cleansia.api.response", qos: .userInitiated)
        CodableHelper.jsonDecoder = ApiDateDecoding.decoder(primary: { CodableHelper.dateFormatter.date(from: $0) })
    }
}
