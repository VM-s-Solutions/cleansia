import CleansiaCore
import UIKit
import UserNotifications

final class PartnerAppDelegate: NSObject, UIApplicationDelegate, UNUserNotificationCenterDelegate {
    weak var registrar: (any PushRegistrar)?
    var onTap: ((PartnerNotificationDestination) -> Void)?

    func application(
        _: UIApplication,
        didFinishLaunchingWithOptions _: [UIApplication.LaunchOptionsKey: Any]? = nil
    ) -> Bool {
        UNUserNotificationCenter.current().delegate = self
        return true
    }

    func application(
        _: UIApplication,
        didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data
    ) {
        let token = deviceToken.apnsHexToken
        let registrar = registrar
        Task { @MainActor in registrar?.reportRegistered(token: token) }
    }

    func application(
        _: UIApplication,
        didFailToRegisterForRemoteNotificationsWithError error: Error
    ) {
        NSLog("APNs registration failed: %@", error.localizedDescription)
    }

    func userNotificationCenter(
        _: UNUserNotificationCenter,
        willPresent _: UNNotification,
        withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void
    ) {
        completionHandler([.banner, .sound])
    }

    func userNotificationCenter(
        _: UNUserNotificationCenter,
        didReceive response: UNNotificationResponse,
        withCompletionHandler completionHandler: @escaping () -> Void
    ) {
        let userInfo = response.notification.request.content.userInfo
        if let destination = PartnerNotificationDeepLink.resolve(userInfo) {
            let onTap = onTap
            Task { @MainActor in onTap?(destination) }
        }
        completionHandler()
    }
}
