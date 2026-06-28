import CleansiaCore
import CleansiaPartnerApi
import Foundation

struct PartnerDeviceRegistrationClient: DeviceRegistrationClient {
    func register(_ request: RegisterDeviceRequest) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerDeviceAPI.deviceRegister(
                registerDeviceCommand: RegisterDeviceCommand(
                    deviceId: request.deviceId,
                    deviceToken: request.deviceToken,
                    platform: request.platform
                )
            )
        }
    }

    func unregister(deviceId: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerDeviceAPI.deviceUnregister(deviceId: deviceId)
        }
    }
}
