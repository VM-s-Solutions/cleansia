import Combine
import XCTest
@testable import CleansiaCore

final class PushSessionObserverTests: XCTestCase {
    private let deviceId = "device-1"

    private func makeRegistrar() -> (PushTokenRegistrar, SpyRegistrationClient) {
        let client = SpyRegistrationClient()
        let registrar = PushTokenRegistrar(
            client: client,
            deviceIdProvider: StubDeviceId(deviceId: deviceId),
            tokenStore: InMemoryTokenStore()
        )
        return (registrar, client)
    }

    func testRegistersWhenSessionAndTokenBothPresent() async {
        let (registrar, client) = makeRegistrar()
        let observer = PushSessionObserver(registrar: registrar)
        let hasSession = CurrentValueSubject<Bool, Never>(true)
        let apnsToken = CurrentValueSubject<String?, Never>("apns-1")

        observer.attach(hasSession: hasSession.eraseToAnyPublisher(), apnsToken: apnsToken.eraseToAnyPublisher())

        await client.waitForRegisters(1)
        XCTAssertEqual(client.registeredTokens, ["apns-1"])
    }

    func testDoesNotRegisterWhenNoSession() async {
        let (registrar, client) = makeRegistrar()
        let observer = PushSessionObserver(registrar: registrar)
        let hasSession = CurrentValueSubject<Bool, Never>(false)
        let apnsToken = CurrentValueSubject<String?, Never>("apns-1")

        observer.attach(hasSession: hasSession.eraseToAnyPublisher(), apnsToken: apnsToken.eraseToAnyPublisher())

        await client.settle()
        XCTAssertEqual(client.registeredTokens, [])
    }

    func testLoginAfterTokenArrivesFiresRegistration() async {
        let (registrar, client) = makeRegistrar()
        let observer = PushSessionObserver(registrar: registrar)
        let hasSession = CurrentValueSubject<Bool, Never>(false)
        let apnsToken = CurrentValueSubject<String?, Never>("apns-1")
        observer.attach(hasSession: hasSession.eraseToAnyPublisher(), apnsToken: apnsToken.eraseToAnyPublisher())

        await client.settle()
        XCTAssertEqual(client.registeredTokens, [])

        hasSession.send(true)

        await client.waitForRegisters(1)
        XCTAssertEqual(client.registeredTokens, ["apns-1"])
    }

    func testSamePairDoesNotRegisterTwice() async {
        let (registrar, client) = makeRegistrar()
        let observer = PushSessionObserver(registrar: registrar)
        let hasSession = CurrentValueSubject<Bool, Never>(true)
        let apnsToken = CurrentValueSubject<String?, Never>("apns-1")
        observer.attach(hasSession: hasSession.eraseToAnyPublisher(), apnsToken: apnsToken.eraseToAnyPublisher())

        await client.waitForRegisters(1)
        apnsToken.send("apns-1")
        await client.settle()

        XCTAssertEqual(client.registerCallCount, 1, "the registrar cache short-circuits the duplicate pair")
    }

    func testSessionWithoutTokenRegistersTokenless() async {
        let (registrar, client) = makeRegistrar()
        let observer = PushSessionObserver(registrar: registrar)
        let hasSession = CurrentValueSubject<Bool, Never>(true)
        let apnsToken = CurrentValueSubject<String?, Never>(nil)

        observer.attach(hasSession: hasSession.eraseToAnyPublisher(), apnsToken: apnsToken.eraseToAnyPublisher())

        await client.waitForRegisters(1)
        XCTAssertEqual(
            client.registeredTokens,
            [""],
            "a live session registers the device before any APNs token exists"
        )
    }

    func testFreshLoginWithoutTokenRegistersTokenless() async {
        let (registrar, client) = makeRegistrar()
        let observer = PushSessionObserver(registrar: registrar)
        let hasSession = CurrentValueSubject<Bool, Never>(false)
        let apnsToken = CurrentValueSubject<String?, Never>(nil)
        observer.attach(hasSession: hasSession.eraseToAnyPublisher(), apnsToken: apnsToken.eraseToAnyPublisher())

        await client.settle()
        XCTAssertEqual(client.registeredTokens, [])

        hasSession.send(true)

        await client.waitForRegisters(1)
        XCTAssertEqual(client.registeredTokens, [""])
    }

    func testTokenArrivalUpgradesTokenlessRegistration() async {
        let (registrar, client) = makeRegistrar()
        let observer = PushSessionObserver(registrar: registrar)
        let hasSession = CurrentValueSubject<Bool, Never>(true)
        let apnsToken = CurrentValueSubject<String?, Never>(nil)
        observer.attach(hasSession: hasSession.eraseToAnyPublisher(), apnsToken: apnsToken.eraseToAnyPublisher())
        await client.waitForRegisters(1)

        apnsToken.send("apns-1")

        await client.waitForRegisters(2)
        XCTAssertEqual(client.registeredTokens, ["", "apns-1"], "a real token re-registers over the token-less row")
    }

    func testSignedOutWithoutTokenDoesNotRegister() async {
        let (registrar, client) = makeRegistrar()
        let observer = PushSessionObserver(registrar: registrar)
        let hasSession = CurrentValueSubject<Bool, Never>(false)
        let apnsToken = CurrentValueSubject<String?, Never>(nil)

        observer.attach(hasSession: hasSession.eraseToAnyPublisher(), apnsToken: apnsToken.eraseToAnyPublisher())

        await client.settle()
        XCTAssertEqual(client.registeredTokens, [])
    }

    func testLogoutAfterTokenlessRegisterStopsReRegistration() async {
        let (registrar, client) = makeRegistrar()
        let observer = PushSessionObserver(registrar: registrar)
        let hasSession = CurrentValueSubject<Bool, Never>(true)
        let apnsToken = CurrentValueSubject<String?, Never>(nil)
        observer.attach(hasSession: hasSession.eraseToAnyPublisher(), apnsToken: apnsToken.eraseToAnyPublisher())
        await client.waitForRegisters(1)

        hasSession.send(false)
        apnsToken.send("apns-2")
        await client.settle()

        XCTAssertEqual(client.registeredTokens, [""], "no register fires while signed out, token-less or not")
    }

    func testLogoutStopsReRegistration() async {
        let (registrar, client) = makeRegistrar()
        let observer = PushSessionObserver(registrar: registrar)
        let hasSession = CurrentValueSubject<Bool, Never>(true)
        let apnsToken = CurrentValueSubject<String?, Never>("apns-1")
        observer.attach(hasSession: hasSession.eraseToAnyPublisher(), apnsToken: apnsToken.eraseToAnyPublisher())
        await client.waitForRegisters(1)

        hasSession.send(false)
        apnsToken.send("apns-2")
        await client.settle()

        XCTAssertEqual(client.registeredTokens, ["apns-1"], "no register fires while signed out")
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
    private var tokens: [String] = []

    var registeredTokens: [String] {
        lock.withLock { tokens }
    }

    var registerCallCount: Int {
        lock.withLock { tokens.count }
    }

    func register(_ request: RegisterDeviceRequest) async -> ApiResult<Void> {
        lock.withLock { tokens.append(request.deviceToken) }
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

    func settle() async {
        try? await Task.sleep(nanoseconds: 50_000_000)
    }
}
