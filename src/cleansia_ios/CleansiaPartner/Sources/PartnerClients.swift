import CleansiaCore
import Foundation

struct PartnerAuthStack {
    let spine: AuthApiClient
    let headerAdapter: HeaderAdapter
    /// The ONE device-id source (D6): the same instance the HeaderAdapter
    /// stamps as X-Device-Id and Device/Register persists. Surfaced so the
    /// Devices client can pass it as currentDeviceId — no second source.
    let deviceIdProvider: DeviceIdProvider
    let signupConsent: SignupConsentRepository
}

enum PartnerAuthSpine {
    static func make(
        apiBaseURL: URL,
        sessionScopedCaches: SessionScopedCacheRegistry
    ) -> PartnerAuthStack {
        let tokenStore = KeychainTokenStore(service: "cz.cleansia.partner.tokens")
        let deviceIdProvider = DeviceIdProvider(service: "cz.cleansia.partner.device")
        let headerAdapter = HeaderAdapter(
            deviceIdProvider: deviceIdProvider,
            anonymousAllowList: .partner
        )
        let signupConsent = SignupConsentRepository(
            store: UserDefaultsSignupConsentStore(),
            client: PartnerSignupConsentClient()
        )
        let spine = AuthApiClient(
            apiBaseURL: apiBaseURL,
            tokenStore: tokenStore,
            headerAdapter: headerAdapter,
            sessionScopedCaches: sessionScopedCaches,
            signupConsent: signupConsent,
            authedSession: URLSession(configuration: .default),
            noAuthSession: URLSession(configuration: .ephemeral)
        )
        return PartnerAuthStack(
            spine: spine,
            headerAdapter: headerAdapter,
            deviceIdProvider: deviceIdProvider,
            signupConsent: signupConsent
        )
    }
}

final class PartnerMobileApiClient: MobileApiClient {
    let baseURL: URL

    init(baseURL: URL) {
        self.baseURL = baseURL
    }
}
