import Foundation

public protocol LastRegisteredTokenStore: AnyObject, Sendable {
    func read() -> String?
    func write(_ token: String)
    func clear()
}

public final class UserDefaultsLastRegisteredTokenStore: LastRegisteredTokenStore, @unchecked Sendable {
    private static let key = "push.last_registered_token"

    private let defaults: UserDefaults

    public init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    public func read() -> String? {
        defaults.string(forKey: Self.key)
    }

    public func write(_ token: String) {
        defaults.set(token, forKey: Self.key)
    }

    public func clear() {
        defaults.removeObject(forKey: Self.key)
    }
}

public actor PushTokenRegistrar: SessionScopedCache {
    private let client: DeviceRegistrationClient
    private let deviceIdProvider: DeviceIdProviding
    private let platform: String
    private let tokenStore: LastRegisteredTokenStore

    public init(
        client: DeviceRegistrationClient,
        deviceIdProvider: DeviceIdProviding,
        platform: String = "ios",
        tokenStore: LastRegisteredTokenStore = UserDefaultsLastRegisteredTokenStore()
    ) {
        self.client = client
        self.deviceIdProvider = deviceIdProvider
        self.platform = platform
        self.tokenStore = tokenStore
    }

    public func ensureRegistered(token: String) async {
        guard token != tokenStore.read() else {
            PushLog.log.notice("device already registered with this token (dedup skip)")
            return
        }
        PushLog.log.notice("registering device (platform=\(platform, privacy: .public))")
        let result = await client.register(
            RegisterDeviceRequest(
                deviceId: deviceIdProvider.deviceId,
                deviceToken: token,
                platform: platform
            )
        )
        switch result {
        case .success:
            PushLog.log.notice("device register SUCCEEDED")
            tokenStore.write(token)
        case let .failure(error):
            PushLog.log.error("device register FAILED: \(String(describing: error), privacy: .public)")
        }
    }

    public func unregisterDevice() async {
        _ = await client.unregister(deviceId: deviceIdProvider.deviceId)
        tokenStore.clear()
    }

    public func clear() async {
        tokenStore.clear()
    }
}
