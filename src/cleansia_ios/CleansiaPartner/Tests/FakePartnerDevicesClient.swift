import CleansiaCore
@testable import CleansiaPartner

@MainActor
final class FakePartnerDevicesClient: PartnerDevicesClient {
    var currentDeviceIdValue = "device-current"
    var myDevicesResult: ApiResult<[UserDevice]> = .success([])
    var revokeResult: ApiResult<Void> = .success(())

    private(set) var currentDeviceIdReads = 0
    private(set) var myDevicesCallCount = 0
    private(set) var revokedRowIds: [String] = []

    /// When set, `revoke` suspends on this until `resumeRevoke()` is called,
    /// so a test can hold one revoke mid-flight and fire a second to exercise
    /// the re-entry guard against real concurrency.
    private var revokeGate: CheckedContinuation<Void, Never>?
    var suspendRevoke = false

    var currentDeviceId: String {
        currentDeviceIdReads += 1
        return currentDeviceIdValue
    }

    func myDevices() async -> ApiResult<[UserDevice]> {
        myDevicesCallCount += 1
        return myDevicesResult
    }

    func revoke(rowId: String) async -> ApiResult<Void> {
        revokedRowIds.append(rowId)
        if suspendRevoke {
            await withCheckedContinuation { continuation in
                revokeGate = continuation
            }
        }
        return revokeResult
    }

    func resumeRevoke() {
        revokeGate?.resume()
        revokeGate = nil
    }
}
