import Foundation

public struct RegisterDeviceRequest: Equatable, Sendable {
    public let deviceId: String
    public let deviceToken: String
    public let platform: String

    public init(deviceId: String, deviceToken: String, platform: String) {
        self.deviceId = deviceId
        self.deviceToken = deviceToken
        self.platform = platform
    }
}

public protocol DeviceRegistrationClient: Sendable {
    func register(_ request: RegisterDeviceRequest) async -> ApiResult<Void>
    func unregister(deviceId: String) async -> ApiResult<Void>
}
