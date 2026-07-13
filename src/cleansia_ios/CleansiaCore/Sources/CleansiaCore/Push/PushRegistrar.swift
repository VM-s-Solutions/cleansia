import Combine
import Foundation

@MainActor
public protocol PushRegistrar: AnyObject {
    var apnsToken: AnyPublisher<String?, Never> { get }
    func requestAuthorization() async -> Bool
    func registerForRemoteNotifications()
    func reportRegistered(token: String)
}

#if canImport(UIKit) && canImport(UserNotifications)
    import UIKit
    import UserNotifications

    @MainActor
    public final class UNUserNotificationPushRegistrar: PushRegistrar {
        private let center: UNUserNotificationCenter
        private let application: UIApplication
        private let tokenSubject = CurrentValueSubject<String?, Never>(nil)

        public init(
            center: UNUserNotificationCenter = .current(),
            application: UIApplication = .shared
        ) {
            self.center = center
            self.application = application
        }

        public var apnsToken: AnyPublisher<String?, Never> {
            tokenSubject.eraseToAnyPublisher()
        }

        public func requestAuthorization() async -> Bool {
            let granted = try? await center.requestAuthorization(options: [.alert, .badge, .sound])
            PushLog.log.notice("notification permission granted=\(granted ?? false, privacy: .public)")
            return granted ?? false
        }

        public func registerForRemoteNotifications() {
            application.registerForRemoteNotifications()
        }

        public func reportRegistered(token: String) {
            PushLog.log.notice("push registration token received (len=\(token.count, privacy: .public))")
            tokenSubject.send(token)
        }
    }
#endif
