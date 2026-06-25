import CleansiaCore
import Foundation

enum PartnerAuthSpine {
    static func make(
        apiBaseURL: URL,
        sessionScopedCaches: SessionScopedCacheRegistry
    ) -> AuthApiClient {
        let tokenStore = KeychainTokenStore(service: "cz.cleansia.partner.tokens")
        let deviceIdProvider = DeviceIdProvider(service: "cz.cleansia.partner.device")
        let headerAdapter = HeaderAdapter(
            deviceIdProvider: deviceIdProvider,
            anonymousAllowList: .partner
        )
        return AuthApiClient(
            apiBaseURL: apiBaseURL,
            tokenStore: tokenStore,
            headerAdapter: headerAdapter,
            sessionScopedCaches: sessionScopedCaches,
            authedSession: URLSession(configuration: .default),
            noAuthSession: URLSession(configuration: .ephemeral)
        )
    }
}

final class PartnerMobileApiClient: MobileApiClient {
    let baseURL: URL

    init(baseURL: URL) {
        self.baseURL = baseURL
    }
}
