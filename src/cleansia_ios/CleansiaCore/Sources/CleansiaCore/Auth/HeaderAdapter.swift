import Foundation
#if canImport(UIKit)
import UIKit
#endif

public struct HeaderAdapter: Sendable {
    private let deviceIdProvider: DeviceIdProviding
    private let deviceLabel: String
    private let timeZoneIdentifier: @Sendable () -> String
    private let anonymousAllowList: AnonymousAllowList

    public init(
        deviceIdProvider: DeviceIdProviding,
        anonymousAllowList: AnonymousAllowList = .partner,
        deviceLabel: String = HeaderAdapter.systemDeviceLabel(),
        timeZoneIdentifier: @escaping @Sendable () -> String = { TimeZone.current.identifier }
    ) {
        self.deviceIdProvider = deviceIdProvider
        self.anonymousAllowList = anonymousAllowList
        self.deviceLabel = deviceLabel
        self.timeZoneIdentifier = timeZoneIdentifier
    }

    public func apply(to request: inout URLRequest, accessToken: String?) {
        request.setValue(headerSafeDeviceId(), forHTTPHeaderField: Header.deviceId)
        request.setValue(headerSafe(deviceLabel, max: 120), forHTTPHeaderField: Header.deviceLabel)
        request.setValue(timeZoneIdentifier(), forHTTPHeaderField: Header.timeZone)

        if let accessToken, !accessToken.isEmpty, !isAnonymous(request.url) {
            request.setValue("Bearer \(accessToken)", forHTTPHeaderField: Header.authorization)
        }
    }

    private func isAnonymous(_ url: URL?) -> Bool {
        guard let path = url?.path else { return false }
        return anonymousAllowList.isAnonymous(path: path)
    }

    private func headerSafeDeviceId() -> String {
        headerSafe(deviceIdProvider.deviceId, max: 64)
    }

    private func headerSafe(_ value: String, max: Int) -> String {
        String(value.unicodeScalars.filter { $0.value >= 32 && $0.value <= 126 }.map(Character.init).prefix(max))
    }

    public static func systemDeviceLabel() -> String {
        #if canImport(UIKit)
        let device = UIDevice.current
        return "\(device.model) - \(device.systemName) \(device.systemVersion)"
        #else
        return "Apple Device"
        #endif
    }

    private enum Header {
        static let authorization = "Authorization"
        static let deviceId = "X-Device-Id"
        static let deviceLabel = "X-Device-Label"
        static let timeZone = "X-Time-Zone"
    }
}
