import Foundation

public struct UserDevice: Equatable, Identifiable {
    public let id: String
    public let platform: String?
    public let deviceId: String?
    public let lastActiveAt: Date?
    public let isCurrent: Bool

    public init(id: String, platform: String?, deviceId: String?, lastActiveAt: Date?, isCurrent: Bool) {
        self.id = id
        self.platform = platform
        self.deviceId = deviceId
        self.lastActiveAt = lastActiveAt
        self.isCurrent = isCurrent
    }
}

public protocol DevicesClient: AnyObject {
    /// The stable per-install id of THIS handset — the SAME instance the
    /// HeaderAdapter stamps as X-Device-Id and Device/Register persisted.
    /// The one source; never a per-call UUID/identifierForVendor.
    var currentDeviceId: String { get }

    func myDevices() async -> ApiResult<[UserDevice]>
    func revoke(rowId: String) async -> ApiResult<Void>
}
