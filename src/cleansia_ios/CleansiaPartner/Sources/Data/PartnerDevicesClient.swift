import CleansiaCore
import CleansiaPartnerApi
import Foundation

final class LivePartnerDevicesClient: DevicesClient, SessionScopedCache {
    private let deviceIdProvider: DeviceIdProviding

    init(deviceIdProvider: DeviceIdProviding) {
        self.deviceIdProvider = deviceIdProvider
    }

    var currentDeviceId: String {
        deviceIdProvider.deviceId
    }

    func myDevices() async -> ApiResult<[UserDevice]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            // Drop malformed rows with no id: an empty rowId is unrevokable
            // (revoke would no-op against DeviceNotFound). The backend always
            // sends an id — this is robustness, not a behavior change.
            try await PartnerDeviceAPI.deviceMine(currentDeviceId: currentDeviceId)
                .compactMap(UserDevice.init)
        }
    }

    func revoke(rowId: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerDeviceAPI.deviceRevoke(deviceRowId: rowId)
        }
    }

    func clear() async {}
}

extension UserDevice {
    init?(_ dto: DeviceDto) {
        guard let id = dto.id, !id.isEmpty else { return nil }
        self.init(
            id: id,
            platform: dto.platform,
            deviceId: dto.deviceId,
            lastActiveAt: dto.lastActiveAt,
            isCurrent: dto.isCurrent ?? false
        )
    }
}
