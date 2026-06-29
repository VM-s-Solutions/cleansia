import CleansiaCore
import Foundation

struct CustomerAuthStack {
    let spine: AuthApiClient
    let headerAdapter: HeaderAdapter
    let deviceIdProvider: DeviceIdProvider
}

enum CustomerAuthSpine {
    static func make(
        apiBaseURL: URL,
        sessionScopedCaches: SessionScopedCacheRegistry
    ) -> CustomerAuthStack {
        let tokenStore = KeychainTokenStore(service: "cz.cleansia.customer.tokens")
        let deviceIdProvider = DeviceIdProvider(service: "cz.cleansia.customer.device")
        let headerAdapter = HeaderAdapter(
            deviceIdProvider: deviceIdProvider,
            anonymousAllowList: .customer
        )
        let spine = AuthApiClient(
            apiBaseURL: apiBaseURL,
            tokenStore: tokenStore,
            headerAdapter: headerAdapter,
            sessionScopedCaches: sessionScopedCaches,
            registerEndpoint: .customer,
            authedSession: URLSession(configuration: .default),
            noAuthSession: URLSession(configuration: .ephemeral)
        )
        return CustomerAuthStack(spine: spine, headerAdapter: headerAdapter, deviceIdProvider: deviceIdProvider)
    }
}

final class CustomerMobileApiClient: MobileApiClient {
    let baseURL: URL

    init(baseURL: URL) {
        self.baseURL = baseURL
    }
}
