import Foundation
import Security

public struct AuthTokens: Equatable, Sendable, Codable {
    public let accessToken: String
    public let accessTokenExpiresAt: Date
    public let refreshToken: String
    public let refreshTokenExpiresAt: Date

    public init(
        accessToken: String,
        accessTokenExpiresAt: Date,
        refreshToken: String,
        refreshTokenExpiresAt: Date
    ) {
        self.accessToken = accessToken
        self.accessTokenExpiresAt = accessTokenExpiresAt
        self.refreshToken = refreshToken
        self.refreshTokenExpiresAt = refreshTokenExpiresAt
    }

    public func isAccessExpired(now: Date = Date()) -> Bool {
        now >= accessTokenExpiresAt
    }

    public func isRefreshExpired(now: Date = Date()) -> Bool {
        now >= refreshTokenExpiresAt
    }
}

public protocol TokenStore: AnyObject, Sendable {
    func current() -> AuthTokens?
    func save(_ tokens: AuthTokens)
    func clear()
}

public final class KeychainTokenStore: TokenStore, @unchecked Sendable {
    private let keychain: KeychainStore
    private let account: String
    private let lock = NSLock()

    public init(
        service: String = "cz.cleansia.auth.tokens",
        account: String = "session",
        accessGroup: String? = nil
    ) {
        keychain = KeychainStore(service: service, accessGroup: accessGroup)
        self.account = account
    }

    public func current() -> AuthTokens? {
        lock.lock()
        defer { lock.unlock() }
        guard let data = keychain.read(account: account) else { return nil }
        return try? JSONDecoder().decode(AuthTokens.self, from: data)
    }

    public func save(_ tokens: AuthTokens) {
        lock.lock()
        defer { lock.unlock() }
        guard let data = try? JSONEncoder().encode(tokens) else { return }
        keychain.write(data, account: account)
    }

    public func clear() {
        lock.lock()
        defer { lock.unlock() }
        keychain.delete(account: account)
    }
}

protocol KeychainStoring: Sendable {
    func read(account: String) -> Data?
    @discardableResult
    func write(_ data: Data, account: String) -> OSStatus
    func delete(account: String)
}

struct KeychainStore: KeychainStoring {
    let service: String
    let accessGroup: String?

    private func baseQuery(account: String) -> [String: Any] {
        var query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account
        ]
        if let accessGroup {
            query[kSecAttrAccessGroup as String] = accessGroup
        }
        return query
    }

    func read(account: String) -> Data? {
        var query = baseQuery(account: account)
        query[kSecReturnData as String] = true
        query[kSecMatchLimit as String] = kSecMatchLimitOne

        var item: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &item)
        guard status == errSecSuccess else { return nil }
        return item as? Data
    }

    @discardableResult
    func write(_ data: Data, account: String) -> OSStatus {
        let query = baseQuery(account: account)
        let attributes: [String: Any] = [
            kSecValueData as String: data,
            kSecAttrAccessible as String: kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
        ]

        let updateStatus = SecItemUpdate(query as CFDictionary, attributes as CFDictionary)
        guard updateStatus == errSecItemNotFound else { return updateStatus }

        var insert = query
        insert.merge(attributes) { _, new in new }
        return SecItemAdd(insert as CFDictionary, nil)
    }

    func delete(account: String) {
        SecItemDelete(baseQuery(account: account) as CFDictionary)
    }
}
