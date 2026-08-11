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

    /// Rotates on every call, like the endpoint, and keeps the tokens it was handed.
    private actor RotatingRefresher: AuthRefreshing {
        private(set) var posted: [String] = []

        func refresh(refreshToken: String) async -> RefreshCallResult {
            posted.append(refreshToken)
            let next = posted.count + 1
            let future = Date(timeIntervalSinceNow: 9999)
            return .refreshed(RefreshedTokens(
                accessToken: "access-\(next)",
                accessTokenExpiresAt: future,
                refreshToken: "r-\(next)",
                refreshTokenExpiresAt: future
            ))
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

    /// The interleaving the coalescing does **not** cover, and must not: a request that goes out after a
    /// refresh landed carries the NEW token, so a 401 on it is the server rejecting *that* token and a
    /// second refresh is the right answer. `refresh(triggeredBy:)`'s stale-token guard short-circuits
    /// only when the caller's token has since been replaced, and here it has not.
    ///
    /// It is pinned because it is the boundary the concurrent test below has to stay off. That test used
    /// to throw 401 on each task's first attempt whatever token the request would have carried, so a task
    /// the scheduler started after the refresh drove a second — correct — refresh and the count came out
    /// 2. The behaviour was never wrong; the fixture could not tell the two cases apart.
    func testA401OnTheAlreadyRefreshedTokenStartsASecondRefresh() async {
        let store = MemTokenStore(tokens(access: "access-1"))
        let refresher = CountingRefresher(newAccessToken: "access-2")
        let bridge = makeBridge(store: store, refresher: refresher)

        for _ in 0 ..< 2 {
            var pending = true
            _ = try? await bridge.executeWithRetry(
                attempt: { () async throws -> Int in
                    if pending {
                        pending = false
                        throw FakeStatus(401)
                    }
                    return 1
                },
                unauthorizedStatus: { ($0 as? FakeStatus)?.code }
            )
        }

        let calls = await refresher.calls
        XCTAssertEqual(calls, 2, "a 401 on the token in the store is a rejection of it, not a duplicate")
    }

    /// Eight requests in flight when the access token expires. They 401 on the **same** token, so the
    /// refresh must happen once: two would race the rotated refresh token, and the loser would be posting
    /// one the server had already retired — a silent sign-out for a customer who did nothing wrong.
    ///
    /// The 401 is driven by the token the request would actually carry, never by a per-task "first
    /// attempt" flag, so the count cannot turn on scheduling. A task the group starts after the refresh
    /// landed reads the new token, does not 401, and never asks for anything — and a task that 401s on
    /// the old one coalesces whether it arrives before `inFlight` is cleared (it awaits that task) or
    /// after (the stale-token guard answers it). Every interleaving yields exactly one call.
    func testConcurrent401sCoalesceIntoOneRefresh() async {
        let store = MemTokenStore(tokens(access: "access-1"))
        let refresher = CountingRefresher(newAccessToken: "access-2")
        let bridge = makeBridge(store: store, refresher: refresher)

        await withTaskGroup(of: Void.self) { group in
            for _ in 0 ..< 8 {
                group.addTask {
                    _ = try? await bridge.executeWithRetry(
                        attempt: { () async throws -> Int in
                            guard bridge.currentAccessToken() == "access-1" else { return 1 }
                            throw FakeStatus(401)
                        },
                        unauthorizedStatus: { ($0 as? FakeStatus)?.code }
                    )
                }
            }
        }

        let calls = await refresher.calls
        XCTAssertEqual(calls, 1)
    }

    /// The property the coalescing exists for, asserted directly rather than inferred from a call count:
    /// **a refresh token is posted once**. The endpoint rotates it, so a second call carrying an
    /// already-spent one is refused and whichever caller lost the race signs the customer out having done
    /// nothing wrong. Two waves, so the claim spans a rotation instead of only the single-refresh case.
    ///
    /// It holds because `SessionRefresher.refresh` has no suspension between reading `inFlight` and
    /// assigning it, and the rotated pair is stored inside the in-flight task — so it is visible to the
    /// stale-token guard before `inFlight` is ever cleared. An `await` inserted between that check and
    /// that assignment reddens this row.
    func testARefreshTokenIsNeverPostedTwice() async {
        let store = MemTokenStore(tokens(access: "access-1"))
        let refresher = RotatingRefresher()
        let bridge = makeBridge(store: store, refresher: refresher)

        for wave in 1 ... 2 {
            let expired = "access-\(wave)"
            await withTaskGroup(of: Void.self) { group in
                for _ in 0 ..< 8 {
                    group.addTask {
                        _ = try? await bridge.executeWithRetry(
                            attempt: { () async throws -> Int in
                                guard bridge.currentAccessToken() == expired else { return 1 }
                                throw FakeStatus(401)
                            },
                            unauthorizedStatus: { ($0 as? FakeStatus)?.code }
                        )
                    }
                }
            }
        }

        let posted = await refresher.posted
        XCTAssertEqual(posted, ["r1", "r-2"])
        XCTAssertEqual(Set(posted).count, posted.count, "a rotated refresh token was posted twice")
    }
}

private struct FakeStatus: Error {
    let code: Int
    init(_ code: Int) {
        self.code = code
    }
}
