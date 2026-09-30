import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The booking flow's membership read over the app's REAL generated `CustomerMembershipAPI`. A past-due
/// or paused enrolment still answers `hasMembership: true` with the plan's cancellation window, while the
/// server charges on the standard window, so the status has to reach the snapshot the quote is built from.
/// A running trial is entitled like a paid month, so its end date changes nothing the booking flow quotes.
@MainActor
final class MembershipSnapshotWireTests: XCTestCase {
    private static let getMinePath = "/api/Membership/GetMine"

    override func setUp() {
        super.setUp()
        MembershipReadStub.reset()
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [MembershipReadStub.self]
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
        CodableHelper.jsonDecoder = ApiDateDecoding.decoder(primary: { CodableHelper.dateFormatter.date(from: $0) })
    }

    override func tearDown() {
        MembershipReadStub.reset()
        super.tearDown()
    }

    func testAPastDueMemberIsLiveWithItsBenefitsPaused() async throws {
        let snapshot = try await readMembership(status: 2)

        XCTAssertTrue(snapshot.hasMembership)
        XCTAssertTrue(snapshot.benefitsPaused)
        XCTAssertEqual(MembershipReadStub.lastRequest?.url?.path, Self.getMinePath)
        XCTAssertEqual(MembershipReadStub.lastRequest?.httpMethod, "GET")
    }

    func testAPausedMemberIsLiveWithItsBenefitsPaused() async throws {
        let snapshot = try await readMembership(status: 4)

        XCTAssertTrue(snapshot.hasMembership)
        XCTAssertTrue(snapshot.benefitsPaused)
    }

    func testAnActiveMemberRunsItsBenefits() async throws {
        let snapshot = try await readMembership(status: 1)

        XCTAssertFalse(snapshot.benefitsPaused)
    }

    func testAPastDueMemberIsQuotedTheStandardWindow() async throws {
        let snapshot = try await readMembership(status: 2)

        let policy = CancellationPolicyBuilder.make(membership: snapshot)

        XCTAssertEqual(policy.freeHours, 24)
        XCTAssertNil(policy.plusFreeHours)
    }

    func testAnActiveMemberIsQuotedThePlanWindow() async throws {
        let snapshot = try await readMembership(status: 1)

        let policy = CancellationPolicyBuilder.make(membership: snapshot)

        XCTAssertEqual(policy.plusFreeHours, 4)
    }

    func testATrialingMemberIsQuotedThePlanWindowAndTheExpressWaiver() async throws {
        let snapshot = try await readMembership(
            status: 1,
            extra: ",\"expressUpgradesPerMonth\":2,\"expressUpgradesRemaining\":2,"
                + "\"trialEndsAtUtc\":\"2099-01-01T00:00:00\""
        )

        XCTAssertEqual(CancellationPolicyBuilder.make(membership: snapshot).plusFreeHours, 4)
        XCTAssertEqual(ExpressWaiverStatus.resolve(snapshot), .available)
    }

    private func readMembership(status: Int, extra: String = "") async throws -> MembershipSnapshot {
        let body = "{\"hasMembership\":true,\"freeCancellationWindowHours\":4,"
            + "\"status\":\(status),\"cancelRequested\":false\(extra)}"
        MembershipReadStub.response = (200, Data(body.utf8))
        return try await LiveMembershipClient().currentMembership().get()
    }
}

private final class MembershipReadStub: URLProtocol {
    private nonisolated(unsafe) static let lock = NSLock()
    private nonisolated(unsafe) static var recorded: URLRequest?
    nonisolated(unsafe) static var response: (Int, Data) = (200, Data("{}".utf8))

    static var lastRequest: URLRequest? {
        lock.withLock { recorded }
    }

    static func reset() {
        lock.withLock { recorded = nil }
        response = (200, Data("{}".utf8))
    }

    override static func canInit(with _: URLRequest) -> Bool {
        true
    }

    override static func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        Self.lock.withLock { Self.recorded = request }
        let (status, data) = Self.response
        guard let url = request.url,
              let response = HTTPURLResponse(
                  url: url,
                  statusCode: status,
                  httpVersion: nil,
                  headerFields: ["Content-Type": "application/json"]
              )
        else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data)
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
