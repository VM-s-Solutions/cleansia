import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct CustomerDeviceRegistrationClient: DeviceRegistrationClient {
    func register(_ request: RegisterDeviceRequest) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await CustomerDeviceAPI.deviceRegister(
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
            _ = try await CustomerDeviceAPI.deviceUnregister(deviceId: deviceId)
        }
    }
}
