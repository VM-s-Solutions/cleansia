import Combine
import Foundation

/// The APNs/FCM token seam between the app delegate and the session-gated
/// registration pipeline. NOTE: actual APNs registration
/// (`UIApplication.registerForRemoteNotifications()`) is called directly in
/// `didFinishLaunching` by each app delegate — iOS silently drops that call
/// when it is deferred into an async flow, so it must never move back here.
@MainActor
public protocol PushRegistrar: AnyObject {
    var apnsToken: AnyPublisher<String?, Never> { get }
    func requestAuthorization() async -> Bool
    func reportRegistered(token: String)
}

#if canImport(UIKit) && canImport(UserNotifications)
    import UIKit
    import UserNotifications

    @MainActor
    public final class UNUserNotificationPushRegistrar: PushRegistrar {
        private let center: UNUserNotificationCenter
        private let tokenSubject = CurrentValueSubject<String?, Never>(nil)

        public init(center: UNUserNotificationCenter = .current()) {
            self.center = center
        }

        public var apnsToken: AnyPublisher<String?, Never> {
            tokenSubject.eraseToAnyPublisher()
        }

        public func requestAuthorization() async -> Bool {
            let granted = try? await center.requestAuthorization(options: [.alert, .badge, .sound])
            return granted ?? false
        }

        public func reportRegistered(token: String) {
            tokenSubject.send(token)
        }
    }
#endif
