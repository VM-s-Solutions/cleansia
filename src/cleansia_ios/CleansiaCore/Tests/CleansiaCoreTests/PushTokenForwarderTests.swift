import Combine
import Foundation
import XCTest
@testable import CleansiaCore

@MainActor
final class PushTokenForwarderTests: XCTestCase {
    private let deviceId = "device-9"

    func testConfiguredTokenRegistersWithPlatformIosAndDeviceId() async {
        let chain = makeChain(isFirebaseConfigured: true)

        chain.forwarder.forward(fcmToken: "fcm-abc")

        await chain.client.waitForRegisters(2)
        // The session-start token-less register and the forwarded-token upgrade are dispatched from two
        // concurrent tasks, so their arrival ORDER is not deterministic (this assertion used to flake on
        // it). Assert order-independently — both must fire with the right deviceId / token / platform.
        XCTAssertEqual(
            chain.client.registeredRequests.sorted { $0.deviceToken < $1.deviceToken },
            [
                RegisterDeviceRequest(deviceId: deviceId, deviceToken: "", platform: "ios"),
                RegisterDeviceRequest(deviceId: deviceId, deviceToken: "fcm-abc", platform: "ios")
            ],
            "both the session-start token-less register and the forwarded-token upgrade fire"
        )
    }

    func testFirebaseNotConfiguredForwardsNothing() async {
        let chain = makeChain(isFirebaseConfigured: false)

        chain.forwarder.forward(fcmToken: "fcm-abc")

        await chain.client.settle()
        XCTAssertEqual(chain.client.registeredTokens, [""], "only the session-start token-less register fires")
    }

    func testNilTokenForwardsNothing() async {
        let chain = makeChain(isFirebaseConfigured: true)

        chain.forwarder.forward(fcmToken: nil)

        await chain.client.settle()
        XCTAssertEqual(chain.client.registeredTokens, [""])
    }

    func testEmptyTokenForwardsNothing() async {
        let chain = makeChain(isFirebaseConfigured: true)

        chain.forwarder.forward(fcmToken: "")

        await chain.client.settle()
        XCTAssertEqual(chain.client.registeredTokens, [""])
    }

    func testRefreshedTokenReRegisters() async {
        let chain = makeChain(isFirebaseConfigured: true)

        chain.forwarder.forward(fcmToken: "fcm-1")
        await chain.client.waitForTokens(["", "fcm-1"])
        chain.forwarder.forward(fcmToken: "fcm-2")
        await chain.client.waitForTokens(["", "fcm-1", "fcm-2"])

        // Arrival order is deliberately NOT asserted, and that is the whole fix. The session-start
        // token-less register races the first forwarded token — the same race the configured-token
        // test above already documents — so ["", "fcm-1", "fcm-2"] was never guaranteed.
        //
        // What this test is actually about is that a REFRESHED token registers again instead of
        // being swallowed as a duplicate. A set plus a count says exactly that, and says it without
        // depending on which of two concurrent tasks reaches the spy first.
        XCTAssertEqual(Set(chain.client.registeredTokens), ["", "fcm-1", "fcm-2"])
        XCTAssertEqual(chain.client.registerCallCount, 3, "each distinct token registers exactly once")
    }

    private func makeChain(isFirebaseConfigured: Bool) -> Chain {
        let registrar = FakePushRegistrar()
        let client = SpyRegistrationClient()
        let tokenRegistrar = PushTokenRegistrar(
            client: client,
            deviceIdProvider: StubDeviceId(deviceId: deviceId),
            tokenStore: InMemoryTokenStore()
        )
        let observer = PushSessionObserver(registrar: tokenRegistrar)
        observer.attach(
            hasSession: CurrentValueSubject<Bool, Never>(true).eraseToAnyPublisher(),
            apnsToken: registrar.apnsToken
        )
        let forwarder = PushTokenForwarder(
            registrar: registrar,
            isFirebaseConfigured: { isFirebaseConfigured }
        )
        return Chain(client: client, forwarder: forwarder, registrar: registrar, observer: observer)
    }

    private struct Chain {
        let client: SpyRegistrationClient
        let forwarder: PushTokenForwarder
        let registrar: FakePushRegistrar
        let observer: PushSessionObserver
    }
}

@MainActor
private final class FakePushRegistrar: PushRegistrar {
    private let subject = CurrentValueSubject<String?, Never>(nil)

    var apnsToken: AnyPublisher<String?, Never> {
        subject.eraseToAnyPublisher()
    }

    func requestAuthorization() async -> Bool {
        true
    }

    func reportRegistered(token: String) {
        subject.send(token)
    }
}

private struct StubDeviceId: DeviceIdProviding {
    let deviceId: String
}

private final class InMemoryTokenStore: LastRegisteredTokenStore, @unchecked Sendable {
    private let lock = NSLock()
    private var value: String?

    func read() -> String? {
        lock.withLock { value }
    }

    func write(_ token: String) {
        lock.withLock { value = token }
    }

    func clear() {
        lock.withLock { value = nil }
    }
}

private final class SpyRegistrationClient: DeviceRegistrationClient, @unchecked Sendable {
    private let lock = NSLock()
    private(set) var registeredRequests: [RegisterDeviceRequest] = []

    var registeredTokens: [String] {
        lock.withLock { registeredRequests.map(\.deviceToken) }
    }

    var registerCallCount: Int {
        lock.withLock { registeredRequests.count }
    }

    func register(_ request: RegisterDeviceRequest) async -> ApiResult<Void> {
        lock.withLock { registeredRequests.append(request) }
        return .success(())
    }

    func unregister(deviceId _: String) async -> ApiResult<Void> {
        .success(())
    }

    func waitForRegisters(_ count: Int) async {
        for _ in 0 ..< 200 {
            if registerCallCount >= count { return }
            try? await Task.sleep(nanoseconds: 5_000_000)
        }
    }

    /// Wait until every expected token has been recorded, whatever order they arrive in.
    ///
    /// Prefer this over `waitForRegisters` wherever the assertion that follows names specific
    /// tokens. A count-based wait returns as soon as *some* n registers have landed, which is not
    /// the same claim: the session-start token-less register and a forwarded token are dispatched
    /// from two concurrent tasks, so "two have arrived" never meant "these two have arrived".
    func waitForTokens(_ expected: Set<String>) async {
        for _ in 0 ..< 200 {
            if expected.isSubset(of: Set(registeredTokens)) { return }
            try? await Task.sleep(nanoseconds: 5_000_000)
        }
    }

    func settle() async {
        try? await Task.sleep(nanoseconds: 50_000_000)
    }
}
