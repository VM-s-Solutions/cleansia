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
            // Confirms the call actually reaches UIKit on the main thread. If this
            // logs but no didRegister/didFail callback follows, the OS/APNs (or the
            // Firebase swizzle) is the culprit, not our call path. Hoisted to a local
            // so the Logger autoclosure doesn't need an explicit `self` capture
            // (which swiftformat would strip, breaking the build).
            let alreadyRegistered = application.isRegisteredForRemoteNotifications
            PushLog.log.notice(
                "requesting APNs registration from the OS (isRegistered before=\(alreadyRegistered, privacy: .public))"
            )
            application.registerForRemoteNotifications()
        }

        public func reportRegistered(token: String) {
            PushLog.log.notice("push registration token received (len=\(token.count, privacy: .public))")
            tokenSubject.send(token)
        }
    }
#endif
