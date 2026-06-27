import CleansiaCore
import CleansiaPartnerApi
import Foundation

struct UserDevice: Equatable, Identifiable {
    let id: String
    let platform: String?
    let deviceId: String?
    let lastActiveAt: Date?
    let isCurrent: Bool
}

protocol PartnerDevicesClient: AnyObject {
    /// The stable per-install id of THIS handset — the SAME instance the
    /// HeaderAdapter stamps as X-Device-Id and Device/Register persisted
    /// (D6). The one source; never a per-call UUID/identifierForVendor.
    var currentDeviceId: String { get }

    func myDevices() async -> ApiResult<[UserDevice]>
    func revoke(rowId: String) async -> ApiResult<Void>
}

final class LivePartnerDevicesClient: PartnerDevicesClient, SessionScopedCache {
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
