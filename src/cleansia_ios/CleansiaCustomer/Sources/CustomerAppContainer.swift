import CleansiaCore
import CleansiaCustomerApi
import Combine
import Foundation
import os

@MainActor
final class CustomerAppContainer: AppContainer {
    private let base: BaseAppContainer
    private let authStack: CustomerAuthStack

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

    var socialAuthClient: SocialAuthClient {
        base.socialAuthClient
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

    lazy var socialSignInProvider: SocialSignInProviding = CustomerSocialSignInProvider(
        googleClientID: AppConfig.googleClientID,
        googleServerClientID: AppConfig.googleServerClientID
    )

    let geocodingService: GeocodingService = CLGeocoderGeocodingService()
    let mapProvider: MapProvider = MapKitMapProvider()

    let orderClient: OrderClient
    let orderEventBus = OrderEventBus()
    let orderRepository: OrderRepository

    let loyaltyRepository: LoyaltyRepository
    let referralRepository: RewardsReferralRepository

    let membershipRepository: MembershipRepository
    let recurringRepository: RecurringBookingRepository

    let disputeRepository: DisputeRepository

    let savedAddressRepository: SavedAddressRepository

    let userProfileRepository: UserProfileRepository
    let devicesClient: CustomerDevicesClient
    let gdprDeleteClient: GdprDeleteClient = LiveGdprDeleteClient()
    let notificationPreferencesClient: NotificationPreferencesClient = LiveNotificationPreferencesClient()
    let changePasswordClient: ChangePasswordClient = LiveChangePasswordClient()

    let pushRegistrar: any PushRegistrar = UNUserNotificationPushRegistrar()
    private let pushTokenRegistrar: PushTokenRegistrar
    private let pushSessionObserver: PushSessionObserver
    private let hasSessionSubject: CurrentValueSubject<Bool, Never>

    init(
        snackbar: SnackbarController,
        apiBaseURL: URL = AppConfig.apiBaseURL
    ) {
        let sessionScopedCaches = SessionScopedCacheRegistry()
        let authStack = CustomerAuthSpine.make(
            apiBaseURL: apiBaseURL,
            sessionScopedCaches: sessionScopedCaches
        )
        self.authStack = authStack
        let orderClient = LiveOrderClient()
        let orderRepository = OrderRepository(client: orderClient)
        self.orderClient = orderClient
        self.orderRepository = orderRepository
        let loyaltyRepository = LoyaltyRepository(client: LiveLoyaltyClient())
        let referralRepository = RewardsReferralRepository(client: LiveRewardsReferralClient())
        self.loyaltyRepository = loyaltyRepository
        self.referralRepository = referralRepository
        let membershipRepository = MembershipRepository(client: LiveMembershipManagementClient())
        let recurringRepository = RecurringBookingRepository(client: LiveRecurringBookingClient())
        self.membershipRepository = membershipRepository
        self.recurringRepository = recurringRepository
        let disputeRepository = DisputeRepository(client: LiveDisputeClient())
        self.disputeRepository = disputeRepository
        let savedAddressRepository = SavedAddressRepository(client: LiveSavedAddressClient())
        self.savedAddressRepository = savedAddressRepository
        let userProfileRepository = UserProfileRepository(
            client: LiveUserProfileClient(tokenStore: authStack.spine.tokenStore)
        )
        self.userProfileRepository = userProfileRepository
        devicesClient = LiveCustomerDevicesClient(deviceIdProvider: authStack.deviceIdProvider)
        let pushTokenRegistrar = PushTokenRegistrar(
            client: CustomerDeviceRegistrationClient(),
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
            makeApiClient: { seams in CustomerMobileApiClient(baseURL: seams.apiBaseURL) }
        )
        sessionScopedCaches.register(orderRepository)
        sessionScopedCaches.register(loyaltyRepository)
        sessionScopedCaches.register(referralRepository)
        sessionScopedCaches.register(membershipRepository)
        sessionScopedCaches.register(recurringRepository)
        sessionScopedCaches.register(disputeRepository)
        sessionScopedCaches.register(savedAddressRepository)
        sessionScopedCaches.register(userProfileRepository)
        sessionScopedCaches.register(pushTokenRegistrar)
        authStack.spine.setPreLogout { [pushTokenRegistrar] in
            await pushTokenRegistrar.unregisterDevice()
        }
    }

    func startPush() {
        hasSessionSubject.send(hasValidSession)
        pushSessionObserver.attach(
            hasSession: hasSessionSubject.eraseToAnyPublisher(),
            apnsToken: pushRegistrar.apnsToken
        )
        Task {
            if await pushRegistrar.requestAuthorization() {
                pushRegistrar.registerForRemoteNotifications()
            }
        }
    }

    func updatePushSession(hasSession: Bool) {
        hasSessionSubject.send(hasSession)
    }

    func installGeneratedClientAuth() {
        #if DEBUG
            let stripeState = StripeConfig.isCardPaymentAvailable ? "configured" : "empty"
            Logger(subsystem: "cz.cleansia.customer", category: "config")
                .debug("Stripe key: \(stripeState, privacy: .public)")
        #endif
        let bridge = GeneratedClientAuthBridge(
            headerAdapter: authStack.headerAdapter,
            tokenStore: authStack.spine.tokenStore,
            sessionRefresher: base.sessionRefresher,
            session: URLSession(configuration: .default)
        )
        CustomerGeneratedAuth.install(bridge: bridge, basePath: apiBaseURL.absoluteString)
        // Response processing + Codable decode happen on this queue instead of
        // main; call sites await via continuations, so UI updates still hop back.
        CleansiaCustomerApiAPI.apiResponseQueue = DispatchQueue(label: "cz.cleansia.api.response", qos: .userInitiated)
        CodableHelper.jsonDecoder = ApiDateDecoding.decoder(primary: { CodableHelper.dateFormatter.date(from: $0) })
    }
}
