import Foundation
import Security

public protocol DeviceIdProviding: Sendable {
    var deviceId: String { get }
}

public final class DeviceIdProvider: DeviceIdProviding, @unchecked Sendable {
    private let keychain: KeychainStoring
    private let account: String
    private let lock = NSLock()
    private var cached: String?
    private var persisted = false

    public convenience init(
        service: String = "cz.cleansia.auth.device",
        account: String = "device_id",
        accessGroup: String? = nil
    ) {
        self.init(
            keychain: KeychainStore(service: service, accessGroup: accessGroup),
            account: account
        )
    }

    init(keychain: KeychainStoring, account: String = "device_id") {
        self.keychain = keychain
        self.account = account
    }

    public var deviceId: String {
        lock.lock()
        defer { lock.unlock() }

        if let cached, persisted { return cached }

        if let data = keychain.read(account: account),
           let existing = String(data: data, encoding: .utf8),
           !existing.isEmpty
        {
            cached = existing
            persisted = true
            return existing
        }

        let id = cached ?? UUID().uuidString
        cached = id
        if let data = id.data(using: .utf8) {
            persisted = keychain.write(data, account: account) == errSecSuccess
        }
        return id
    }
}
