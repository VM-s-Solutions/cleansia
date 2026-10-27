import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The serviced-city read over the app's REAL generated `CustomerServiceCityAPI`. The Prague rows' ZIP prefix has to
/// reach the city rule: without it a Prague address named Прага, which the server books by its postcode, reads as a
/// city we do not serve.
@MainActor
final class ServiceCityWireTests: XCTestCase {
    override func setUp() {
        super.setUp()
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [ServiceAreaReadStub.self]
        let session = URLSession(configuration: config)
        let tokenStore = SignedInTokenStore()
        CustomerGeneratedAuth.install(
            bridge: GeneratedClientAuthBridge(
                headerAdapter: HeaderAdapter(deviceIdProvider: WireDeviceId(), anonymousAllowList: .customer),
                tokenStore: tokenStore,
                sessionRefresher: SessionRefresher(
                    tokenStore: tokenStore,
                    refreshClient: NeverRefreshing(),
                    sessionManager: SessionManager(),
                    sessionScopedCaches: SessionScopedCacheRegistry()
                ),
                session: session
            ),
            basePath: "https://api.test"
        )
    }

    func testEachRowKeepsItsZipPrefix() async throws {
        let cities = try await CustomerServiceAreaDataSource().fetchServicedCities(countryId: nil).get()

        XCTAssertEqual(cities, [
            ServicedCity(id: "praha", countryId: "cz-id", name: "Praha", zipPrefix: "1"),
            ServicedCity(id: "brno", countryId: "cz-id", name: "Brno", zipPrefix: nil)
        ])
    }

    func testAPragueAddressNamedInCyrillicIsServicedByItsPostcode() async {
        let serviced = await AddressServiceability.cityServiced(
            provider: ServiceAreaProvider(dataSource: CustomerServiceAreaDataSource()),
            countryIsoCode: "cz",
            city: "Прага",
            zipCode: "110 00"
        )

        XCTAssertEqual(serviced, true)
    }
}

private final class ServiceAreaReadStub: URLProtocol {
    private static let bodies = [
        "/api/Country/GetServiced": #"[{"id":"cz-id","isoCode":"CZE","name":"Czechia"}]"#,
        "/api/ServiceCity": #"""
        [{"id":"praha","countryId":"cz-id","name":"Praha","zipPrefix":"1"},
         {"id":"brno","countryId":"cz-id","name":"Brno"}]
        """#
    ]

    override static func canInit(with _: URLRequest) -> Bool {
        true
    }

    override static func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        guard let url = request.url,
              let body = Self.bodies[url.path],
              let response = HTTPURLResponse(
                  url: url,
                  statusCode: 200,
                  httpVersion: nil,
                  headerFields: ["Content-Type": "application/json"]
              )
        else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: Data(body.utf8))
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}
}

private struct WireDeviceId: DeviceIdProviding {
    var deviceId: String {
        "device-wire"
    }
}

private struct NeverRefreshing: AuthRefreshing {
    func refresh(refreshToken _: String) async -> RefreshCallResult {
        .retryable
    }
}

private final class SignedInTokenStore: TokenStore, @unchecked Sendable {
    func current() -> AuthTokens? {
        AuthTokens(
            accessToken: "access-1",
            accessTokenExpiresAt: Date(timeIntervalSinceNow: 900),
            refreshToken: "refresh-1",
            refreshTokenExpiresAt: Date(timeIntervalSinceNow: 9999)
        )
    }

    func save(_: AuthTokens) {}

    func clear() {}
}
