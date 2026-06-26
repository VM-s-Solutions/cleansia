import Foundation
import Security
#if canImport(UIKit)
    import UIKit
#endif

public protocol DeviceIdProviding: Sendable {
    var deviceId: String { get }
}

public final class DeviceIdProvider: DeviceIdProviding, @unchecked Sendable {
    private let keychain: KeychainStore
    private let account: String
    private let lock = NSLock()
    private var cached: String?
    private let fallbackVendorId: () -> String?

    public init(
        service: String = "cz.cleansia.auth.device",
        account: String = "device_id",
        accessGroup: String? = nil,
        vendorId: @escaping () -> String? = DeviceIdProvider.vendorIdentifier
    ) {
        keychain = KeychainStore(service: service, accessGroup: accessGroup)
        self.account = account
        fallbackVendorId = vendorId
    }

    public var deviceId: String {
        lock.lock()
        defer { lock.unlock() }
        if let cached { return cached }

        if let data = keychain.read(account: account),
           let existing = String(data: data, encoding: .utf8),
           !existing.isEmpty
        {
            cached = existing
            return existing
        }

        let generated = fallbackVendorId() ?? UUID().uuidString
        if let data = generated.data(using: .utf8) {
            keychain.write(data, account: account)
        }
        cached = generated
        return generated
    }

    public static func vendorIdentifier() -> String? {
        #if canImport(UIKit)
            return UIDevice.current.identifierForVendor?.uuidString
        #else
            return nil
        #endif
    }
}
