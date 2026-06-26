import CleansiaCore
import Foundation

struct PartnerAuthStack {
    let spine: AuthApiClient
    let headerAdapter: HeaderAdapter
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
        let spine = AuthApiClient(
            apiBaseURL: apiBaseURL,
            tokenStore: tokenStore,
            headerAdapter: headerAdapter,
            sessionScopedCaches: sessionScopedCaches,
            authedSession: URLSession(configuration: .default),
            noAuthSession: URLSession(configuration: .ephemeral)
        )
        return PartnerAuthStack(spine: spine, headerAdapter: headerAdapter)
    }
}

final class PartnerMobileApiClient: MobileApiClient {
    let baseURL: URL

    init(baseURL: URL) {
        self.baseURL = baseURL
    }
}
