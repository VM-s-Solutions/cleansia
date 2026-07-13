import CleansiaCore
import FirebaseCore
import FirebaseMessaging
import UIKit
import UserNotifications

final class CustomerAppDelegate: NSObject, UIApplicationDelegate, UNUserNotificationCenterDelegate, MessagingDelegate {
    weak var registrar: (any PushRegistrar)?
    private(set) var firebaseConfigured = false

    func application(
        _: UIApplication,
        didFinishLaunchingWithOptions _: [UIApplication.LaunchOptionsKey: Any]? = nil
    ) -> Bool {
        if GoogleServicePlist.isPresent {
            FirebaseApp.configure()
            Messaging.messaging().delegate = self
            firebaseConfigured = true
            PushLog.log.notice("Firebase configured (GoogleService-Info.plist present)")
        } else {
            PushLog.log.error("Firebase NOT configured: GoogleService-Info.plist missing from the app bundle")
        }
        UNUserNotificationCenter.current().delegate = self
        return true
    }

    func application(
        _: UIApplication,
        didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data
    ) {
        guard firebaseConfigured else {
            PushLog.log.error("APNs token arrived but Firebase is not configured")
            return
        }
        PushLog.log.notice("APNs device token received; handed to Firebase Messaging")
        Messaging.messaging().apnsToken = deviceToken
    }

    func application(
        _: UIApplication,
        didFailToRegisterForRemoteNotificationsWithError error: Error
    ) {
        NSLog("APNs registration failed: %@", error.localizedDescription)
    }

    func messaging(_: Messaging, didReceiveRegistrationToken fcmToken: String?) {
        guard let registrar else { return }
        let forwarder = PushTokenForwarder(
            registrar: registrar,
            isFirebaseConfigured: { [weak self] in self?.firebaseConfigured ?? false }
        )
        Task { @MainActor in forwarder.forward(fcmToken: fcmToken) }
    }

    func userNotificationCenter(
        _: UNUserNotificationCenter,
        willPresent _: UNNotification,
        withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void
    ) {
        completionHandler([.banner, .sound])
    }
}
