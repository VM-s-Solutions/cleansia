import CleansiaCore
import Foundation

enum CustomerAuthSpine {
    static func make(
        apiBaseURL: URL,
        sessionScopedCaches: SessionScopedCacheRegistry
    ) -> AuthApiClient {
        let tokenStore = KeychainTokenStore(service: "cz.cleansia.customer.tokens")
        let deviceIdProvider = DeviceIdProvider(service: "cz.cleansia.customer.device")
        let headerAdapter = HeaderAdapter(
            deviceIdProvider: deviceIdProvider,
            anonymousAllowList: .customer
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

final class CustomerMobileApiClient: MobileApiClient {
    let baseURL: URL

    init(baseURL: URL) {
        self.baseURL = baseURL
    }
}
