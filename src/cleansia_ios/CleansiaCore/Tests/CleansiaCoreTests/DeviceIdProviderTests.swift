import Foundation
import Security
import XCTest
@testable import CleansiaCore

final class DeviceIdProviderTests: XCTestCase {
    func testPersistedIdIsAFreshlyGeneratedUuid() {
        let keychain = StubKeychain()
        let provider = DeviceIdProvider(keychain: keychain, account: "device_id")

        let id = provider.deviceId

        XCTAssertNotNil(UUID(uuidString: id), "the stored id must be a valid generated UUID string")
    }

    func testEachInstallMintsItsOwnIdNotADeviceWideConstant() {
        let firstInstall = DeviceIdProvider(keychain: StubKeychain(), account: "device_id").deviceId
        let secondInstall = DeviceIdProvider(keychain: StubKeychain(), account: "device_id").deviceId

        XCTAssertNotEqual(
            firstInstall,
            secondInstall,
            "the id is minted per install, not derived from a device-wide identity like IDFV"
        )
    }

    func testWritesGeneratedIdToKeychainOnFirstRead() throws {
        let keychain = StubKeychain()
        let provider = DeviceIdProvider(keychain: keychain, account: "device_id")

        let id = provider.deviceId

        let stored = try XCTUnwrap(keychain.read(account: "device_id"))
        XCTAssertEqual(String(bytes: stored, encoding: .utf8), id)
    }

    func testReturnsStoredValueOnSubsequentReadsWithoutRegenerating() {
        let keychain = StubKeychain()
        let provider = DeviceIdProvider(keychain: keychain, account: "device_id")

        let first = provider.deviceId
        let second = provider.deviceId

        XCTAssertEqual(first, second)
        XCTAssertEqual(keychain.writeCount, 1, "a persisted id must not be re-written on every read")
    }

    func testStableAcrossFreshProviderBackedBySameStore() {
        let keychain = StubKeychain()
        let first = DeviceIdProvider(keychain: keychain, account: "device_id").deviceId

        let second = DeviceIdProvider(keychain: keychain, account: "device_id").deviceId

        XCTAssertEqual(first, second, "a second provider over the same store must return the same id")
    }

    func testIdIsStableInSessionWhilePersistenceFails() {
        let keychain = StubKeychain()
        keychain.writeStatus = errSecInteractionNotAllowed
        let provider = DeviceIdProvider(keychain: keychain, account: "device_id")

        let first = provider.deviceId
        let second = provider.deviceId

        XCTAssertEqual(first, second, "a failed write must not re-mint; the id stays constant for the process")
        XCTAssertNotNil(UUID(uuidString: first))
    }

    func testRetriesPersistWhileKeychainIsInaccessibleWithoutReminting() {
        let keychain = StubKeychain()
        keychain.writeStatus = errSecInteractionNotAllowed
        let provider = DeviceIdProvider(keychain: keychain, account: "device_id")

        let first = provider.deviceId
        let second = provider.deviceId

        XCTAssertEqual(first, second)
        XCTAssertEqual(keychain.writeCount, 2, "the persist is re-attempted each read during the outage")
    }

    func testConvergesOnTheSameInMemoryIdOnceKeychainRecovers() {
        let keychain = StubKeychain()
        keychain.writeStatus = errSecInteractionNotAllowed
        let provider = DeviceIdProvider(keychain: keychain, account: "device_id")

        let duringOutage = provider.deviceId

        keychain.writeStatus = errSecSuccess
        let afterRecovery = provider.deviceId

        XCTAssertEqual(afterRecovery, duringOutage, "recovery persists the original id, never a new UUID")
        let persisted = try? XCTUnwrap(keychain.read(account: "device_id"))
        XCTAssertEqual(persisted.flatMap { String(bytes: $0, encoding: .utf8) }, duringOutage)
        let nextLaunch = DeviceIdProvider(keychain: keychain, account: "device_id").deviceId
        XCTAssertEqual(nextLaunch, duringOutage, "the next launch reads the same converged id")
    }
}

private final class StubKeychain: KeychainStoring, @unchecked Sendable {
    private let lock = NSLock()
    private var storage: [String: Data] = [:]
    private(set) var writeCount = 0
    var writeStatus: OSStatus = errSecSuccess

    func read(account: String) -> Data? {
        lock.lock()
        defer { lock.unlock() }
        return storage[account]
    }

    func write(_ data: Data, account: String) -> OSStatus {
        lock.lock()
        defer { lock.unlock() }
        writeCount += 1
        guard writeStatus == errSecSuccess else { return writeStatus }
        storage[account] = data
        return errSecSuccess
    }

    func delete(account: String) {
        lock.lock()
        defer { lock.unlock() }
        storage[account] = nil
    }
}
