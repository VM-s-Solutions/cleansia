import XCTest
@testable import CleansiaCore

final class SessionScopedCacheRegistryTests: XCTestCase {
    private final class SpyCache: SessionScopedCache {
        private(set) var clearCount = 0
        func clear() async { clearCount += 1 }
    }

    func testClearAllFlushesEveryRegisteredCache() async {
        let registry = SessionScopedCacheRegistry()
        let a = SpyCache()
        let b = SpyCache()
        registry.register(a)
        registry.register(b)

        await registry.clearAll()

        XCTAssertEqual(a.clearCount, 1)
        XCTAssertEqual(b.clearCount, 1)
    }

    func testEmptyRegistryClearsWithoutError() async {
        let registry = SessionScopedCacheRegistry()
        await registry.clearAll()
    }

    func testRegisteredCacheIsHeldWeaklyDoesNotRetain() async {
        let registry = SessionScopedCacheRegistry()
        var cache: SpyCache? = SpyCache()
        weak var weakCache = cache
        if let cache { registry.register(cache) }

        cache = nil
        XCTAssertNil(weakCache)

        await registry.clearAll()
    }
}

@MainActor
final class AppEnvironmentTests: XCTestCase {
    private final class StubContainer: AppContainer {
        let apiBaseURL: URL
        let snackbar: SnackbarController
        let sessionScopedCaches: SessionScopedCacheRegistry
        let sessionManager: SessionManager
        let authClient: AuthClient
        let refreshClient: RefreshClient
        let sessionRefresher: SessionRefresher

        init(
            apiBaseURL: URL,
            snackbar: SnackbarController,
            sessionScopedCaches: SessionScopedCacheRegistry,
            authClient: AuthClient,
            refreshClient: RefreshClient
        ) {
            self.apiBaseURL = apiBaseURL
            self.snackbar = snackbar
            self.sessionScopedCaches = sessionScopedCaches
            sessionManager = SessionManager(sessionScopedCaches: sessionScopedCaches)
            self.authClient = authClient
            self.refreshClient = refreshClient
            sessionRefresher = SessionRefresher(
                tokenStore: StubTokenStore(),
                refreshClient: refreshClient,
                sessionManager: sessionManager,
                sessionScopedCaches: sessionScopedCaches
            )
        }
    }

    func testContainerExposesItsConfiguredSeams() throws {
        let url = try XCTUnwrap(URL(string: "https://api.example.test"))
        let snackbar = SnackbarController()
        let registry = SessionScopedCacheRegistry()
        let auth = StubAuthClient()
        let refresh = StubRefreshClient()
        let container = StubContainer(
            apiBaseURL: url,
            snackbar: snackbar,
            sessionScopedCaches: registry,
            authClient: auth,
            refreshClient: refresh
        )

        XCTAssertEqual(container.apiBaseURL, url)
        XCTAssertTrue(container.snackbar === snackbar)
        XCTAssertTrue(container.sessionScopedCaches === registry)
        XCTAssertTrue(container.authClient === auth)
        XCTAssertTrue(container.refreshClient === refresh)
    }
}

final class LazyRefreshSessionBoundaryTests: XCTestCase {
    func testRefreshSessionIsBuiltSeparatelyAndLazily() {
        var noAuthBuildCount = 0
        var authBuildCount = 0

        let boundary = AuthNetworkBoundary(
            makeRefreshSession: {
                noAuthBuildCount += 1
                return ()
            },
            makeAuthedSession: {
                authBuildCount += 1
                return ()
            }
        )

        XCTAssertEqual(noAuthBuildCount, 0)
        XCTAssertEqual(authBuildCount, 0)

        _ = boundary.refreshSession
        _ = boundary.refreshSession
        _ = boundary.authedSession
        _ = boundary.authedSession

        XCTAssertEqual(noAuthBuildCount, 1)
        XCTAssertEqual(authBuildCount, 1)
    }

    func testRefreshAndAuthedSessionsAreDistinctInstances() {
        final class Session {}
        let boundary = AuthNetworkBoundary(
            makeRefreshSession: { Session() },
            makeAuthedSession: { Session() }
        )

        XCTAssertFalse(boundary.refreshSession === boundary.authedSession)
    }
}

@MainActor
final class BaseAppContainerTests: XCTestCase {
    private func makeContainer(
        spineBuilds: @escaping () -> Void = {},
        apiBuilds: @escaping () -> Void = {}
    ) throws -> BaseAppContainer {
        let url = try XCTUnwrap(URL(string: "https://api.example.test"))
        return BaseAppContainer(
            apiBaseURL: url,
            snackbar: SnackbarController(),
            makeAuthSpine: { _ in spineBuilds(); return StubAuthSpine() },
            makeApiClient: { seams in apiBuilds(); return StubApiClient(baseURL: seams.apiBaseURL) }
        )
    }

    func testSnackbarAndCachesAreSharedSingletons() throws {
        let container = try makeContainer()
        XCTAssertTrue(container.snackbar === container.snackbar)
        XCTAssertTrue(container.sessionScopedCaches === container.sessionScopedCaches)
    }

    func testAuthSpineAndApiClientsAreBuiltOnceLazily() throws {
        var spineBuilds = 0
        var apiBuilds = 0
        let container = try makeContainer(
            spineBuilds: { spineBuilds += 1 },
            apiBuilds: { apiBuilds += 1 }
        )

        XCTAssertEqual(spineBuilds, 0)
        XCTAssertEqual(apiBuilds, 0)

        _ = container.authClient
        _ = container.authClient
        _ = container.apiClient
        _ = container.apiClient

        XCTAssertEqual(spineBuilds, 1)
        XCTAssertEqual(apiBuilds, 1)
    }

    func testAuthAndRefreshClientShareTheSameSpineInstance() throws {
        let container = try makeContainer()
        XCTAssertTrue(container.authClient === container.refreshClient)
    }

    func testApiClientReceivesTheConfiguredBaseURL() throws {
        let container = try makeContainer()
        XCTAssertEqual(container.apiClient.baseURL, container.apiBaseURL)
    }
}

private final class StubAuthSpine: AuthSpine, @unchecked Sendable {
    let tokenStore: TokenStore = StubTokenStore()
    func signOutLocal() async {}
    func logout() async {}
    func refresh(refreshToken _: String) async -> RefreshedTokens? { nil }
}

private final class StubTokenStore: TokenStore, @unchecked Sendable {
    func current() -> AuthTokens? { nil }
    func save(_: AuthTokens) {}
    func clear() {}
}

private final class StubAuthClient: AuthClient {
    func signOutLocal() async {}
    func logout() async {}
}

private final class StubRefreshClient: RefreshClient {
    func refresh(refreshToken _: String) async -> RefreshedTokens? { nil }
}

private final class StubApiClient: MobileApiClient {
    let baseURL: URL
    init(baseURL: URL) { self.baseURL = baseURL }
}
