import CleansiaCore
@testable import CleansiaPartner

/// The devices the partner's captures show; the devices screen itself is tested in Core.
@MainActor
final class FakePartnerDevicesClient: DevicesClient {
    var myDevicesResult: ApiResult<[UserDevice]> = .success([])

    var currentDeviceId: String {
        "device-current"
    }

    func myDevices() async -> ApiResult<[UserDevice]> {
        myDevicesResult
    }

    func revoke(rowId _: String) async -> ApiResult<Void> {
        .success(())
    }
}
