import XCTest
@testable import CleansiaCore

@MainActor
final class GeneratedClientAuthBridgeTests: XCTestCase {
    private struct FixedDeviceId: DeviceIdProviding {
        var deviceId: String {
            "device-1"
        }
    }

    private actor CountingRefresher: AuthRefreshing {
        private(set) var calls = 0
        let newAccessToken: String

        init(newAccessToken: String) {
            self.newAccessToken = newAccessToken
        }

        func refresh(refreshToken _: String) async -> RefreshCallResult {
            calls += 1
            let future = Date(timeIntervalSinceNow: 9999)
            return .refreshed(RefreshedTokens(
                accessToken: newAccessToken,
                accessTokenExpiresAt: future,
                refreshToken: "r-rotated",
                refreshTokenExpiresAt: future
            ))
        }
    }

    private struct RetryableRefresher: AuthRefreshing {
        func refresh(refreshToken _: String) async -> RefreshCallResult {
            .retryable
        }
    }

    private final class MemTokenStore: TokenStore, @unchecked Sendable {
        private let lock = NSLock()
        private var stored: AuthTokens?
        init(_ tokens: AuthTokens?) {
            stored = tokens
        }

        func current() -> AuthTokens? {
            lock.lock()
            defer { lock.unlock() }
            return stored
        }

        func save(_ tokens: AuthTokens) {
            lock.lock()
            stored = tokens
            lock.unlock()
        }

        func clear() {
            lock.lock()
            stored = nil
            lock.unlock()
        }
    }

    private func tokens(access: String) -> AuthTokens {
        let future = Date(timeIntervalSinceNow: 9999)
        return AuthTokens(
            accessToken: access,
            accessTokenExpiresAt: future,
            refreshToken: "r1",
            refreshTokenExpiresAt: future
        )
    }

    private func makeBridge(store: TokenStore, refresher: AuthRefreshing) -> GeneratedClientAuthBridge {
        let sessionRefresher = SessionRefresher(
            tokenStore: store,
            refreshClient: refresher,
            sessionManager: SessionManager(),
            sessionScopedCaches: SessionScopedCacheRegistry()
        )
        return GeneratedClientAuthBridge(
            headerAdapter: HeaderAdapter(
                deviceIdProvider: FixedDeviceId(),
                deviceLabel: "iPhone",
                timeZoneIdentifier: { "Europe/Prague" }
            ),
            tokenStore: store,
            sessionRefresher: sessionRefresher
        )
    }

    func testAuthorizeStampsBearerAndDeviceHeaders() throws {
        let store = MemTokenStore(tokens(access: "access-1"))
        let bridge = makeBridge(store: store, refresher: CountingRefresher(newAccessToken: "x"))
        var request = try URLRequest(url: XCTUnwrap(URL(string: "https://api.test/api/Dashboard/GetStats")))

        bridge.authorize(&request)

        XCTAssertEqual(request.value(forHTTPHeaderField: "Authorization"), "Bearer access-1")
        XCTAssertEqual(request.value(forHTTPHeaderField: "X-Device-Id"), "device-1")
        XCTAssertEqual(request.value(forHTTPHeaderField: "X-Device-Label"), "iPhone")
        XCTAssertEqual(request.value(forHTTPHeaderField: "X-Time-Zone"), "Europe/Prague")
    }

    func testExecuteWithRetryRetriesOnceOn401AfterRefresh() async {
        let store = MemTokenStore(tokens(access: "access-1"))
        let refresher = CountingRefresher(newAccessToken: "access-2")
        let bridge = makeBridge(store: store, refresher: refresher)

        var attempts = 0
        let result: Int? = try? await bridge.executeWithRetry(
            attempt: {
                attempts += 1
                if attempts == 1 { throw FakeStatus(401) }
                return 7
            },
            unauthorizedStatus: { ($0 as? FakeStatus)?.code }
        )

        XCTAssertEqual(result, 7)
        XCTAssertEqual(attempts, 2)
        let calls = await refresher.calls
        XCTAssertEqual(calls, 1)
        XCTAssertEqual(store.current()?.accessToken, "access-2")
    }

    func testExecuteWithRetryDoesNotRetryNon401() async {
        let store = MemTokenStore(tokens(access: "access-1"))
        let refresher = CountingRefresher(newAccessToken: "access-2")
        let bridge = makeBridge(store: store, refresher: refresher)

        var attempts = 0
        do {
            _ = try await bridge.executeWithRetry(
                attempt: { () async throws -> Int in
                    attempts += 1
                    throw FakeStatus(500)
                },
                unauthorizedStatus: { ($0 as? FakeStatus)?.code }
            )
            XCTFail("expected throw")
        } catch {
            XCTAssertEqual((error as? FakeStatus)?.code, 500)
        }
        XCTAssertEqual(attempts, 1)
        let calls = await refresher.calls
        XCTAssertEqual(calls, 0)
    }

    func testExecuteWithRetrySurfacesOriginal401WhenRefreshIsRetryable() async {
        let store = MemTokenStore(tokens(access: "access-1"))
        let bridge = makeBridge(store: store, refresher: RetryableRefresher())

        var attempts = 0
        do {
            _ = try await bridge.executeWithRetry(
                attempt: { () async throws -> Int in
                    attempts += 1
                    throw FakeStatus(401)
                },
                unauthorizedStatus: { ($0 as? FakeStatus)?.code }
            )
            XCTFail("expected throw")
        } catch {
            XCTAssertEqual((error as? FakeStatus)?.code, 401)
        }
        XCTAssertEqual(attempts, 1)
        XCTAssertEqual(store.current()?.accessToken, "access-1", "tokens survive a retryable refresh failure")
    }

    func testConcurrent401sCoalesceIntoOneRefresh() async {
        let store = MemTokenStore(tokens(access: "access-1"))
        let refresher = CountingRefresher(newAccessToken: "access-2")
        let bridge = makeBridge(store: store, refresher: refresher)

        await withTaskGroup(of: Void.self) { group in
            for _ in 0 ..< 8 {
                group.addTask {
                    let firstAttempt = Locked(true)
                    _ = try? await bridge.executeWithRetry(
                        attempt: { () async throws -> Int in
                            if firstAttempt.swapFalse() { throw FakeStatus(401) }
                            return 1
                        },
                        unauthorizedStatus: { ($0 as? FakeStatus)?.code }
                    )
                }
            }
        }

        let calls = await refresher.calls
        XCTAssertEqual(calls, 1)
    }
}

private struct FakeStatus: Error {
    let code: Int
    init(_ code: Int) {
        self.code = code
    }
}

private final class Locked: @unchecked Sendable {
    private let lock = NSLock()
    private var value: Bool
    init(_ value: Bool) {
        self.value = value
    }

    func swapFalse() -> Bool {
        lock.lock()
        defer { lock.unlock() }
        let old = value
        value = false
        return old
    }
}
