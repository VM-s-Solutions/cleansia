import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct UserDevice: Equatable, Identifiable {
    let id: String
    let platform: String?
    let deviceId: String?
    let lastActiveAt: Date?
    let isCurrent: Bool
}

protocol CustomerDevicesClient: AnyObject {
    var currentDeviceId: String { get }
    func myDevices() async -> ApiResult<[UserDevice]>
    func revoke(rowId: String) async -> ApiResult<Void>
}

final class LiveCustomerDevicesClient: CustomerDevicesClient {
    private let deviceIdProvider: DeviceIdProviding

    init(deviceIdProvider: DeviceIdProviding) {
        self.deviceIdProvider = deviceIdProvider
    }

    var currentDeviceId: String {
        deviceIdProvider.deviceId
    }

    func myDevices() async -> ApiResult<[UserDevice]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerDeviceAPI.deviceMine(currentDeviceId: currentDeviceId)
                .compactMap(UserDevice.init)
        }
    }

    func revoke(rowId: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            let response = try await CustomerDeviceAPI.deviceRevoke(deviceRowId: rowId)
            if response.success == false {
                throw ApiError(code: "device.revoke_failed")
            }
        }
    }
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
