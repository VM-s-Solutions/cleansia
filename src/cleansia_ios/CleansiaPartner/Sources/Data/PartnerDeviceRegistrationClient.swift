import CleansiaCore
import CleansiaPartnerApi
import Foundation

struct PartnerDeviceRegistrationClient: DeviceRegistrationClient {
    func register(_ request: RegisterDeviceRequest) async -> ApiResult<Void> {
        let idPrefix = String(request.deviceId.prefix(8))
        let tokenLen = request.deviceToken.count
        PushLog.log.notice(
            "HTTP POST Device/Register firing (deviceId=\(idPrefix, privacy: .public)…, tokenLen=\(tokenLen, privacy: .public), platform=\(request.platform, privacy: .public))"
        )
        let result: ApiResult<Void> = await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerDeviceAPI.deviceRegister(
                registerDeviceCommand: RegisterDeviceCommand(
                    deviceId: request.deviceId,
                    deviceToken: request.deviceToken,
                    platform: request.platform
                )
            )
        }
        switch result {
        case .success:
            PushLog.log.notice("HTTP POST Device/Register -> OK")
        case let .failure(error):
            PushLog.log.error(
                "HTTP POST Device/Register FAILED: status=\(String(describing: error.httpStatus), privacy: .public) code=\(error.code ?? "-", privacy: .public) message=\(error.message ?? "-", privacy: .public)"
            )
        }
        return result
    }

    func unregister(deviceId: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerDeviceAPI.deviceUnregister(deviceId: deviceId)
        }
    }
}
