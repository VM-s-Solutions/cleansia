import CleansiaCore
import FirebaseCore
import FirebaseMessaging
import UIKit
import UserNotifications

final class PartnerAppDelegate: NSObject, UIApplicationDelegate, UNUserNotificationCenterDelegate, MessagingDelegate {
    weak var registrar: (any PushRegistrar)?
    var onTap: ((PartnerNotificationDestination) -> Void)?
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

    /// Explicitly asks Firebase for the current FCM registration token and forwards it.
    /// The `didReceiveRegistrationToken` delegate only fires on a NEW/refreshed token, so a
    /// cached token (common after re-install) never triggers registration; this pulls it
    /// proactively. Firebase resolves the token once the swizzled APNs token is in place;
    /// on failure it logs the concrete reason (e.g. no APNs token = a provisioning gap).
    func requestFcmToken() {
        guard firebaseConfigured else {
            PushLog.log.error("FCM token requested but Firebase is not configured")
            return
        }
        Messaging.messaging().token { [weak self] token, error in
            if let error {
                PushLog.log.error("FCM token fetch FAILED: \(String(describing: error), privacy: .public)")
                return
            }
            guard let token, !token.isEmpty else {
                PushLog.log.error("FCM token fetch returned empty")
                return
            }
            PushLog.log.notice("FCM token fetched explicitly (len=\(token.count, privacy: .public))")
            guard let self, let registrar else { return }
            let forwarder = PushTokenForwarder(
                registrar: registrar,
                isFirebaseConfigured: { [weak self] in self?.firebaseConfigured ?? false }
            )
            Task { @MainActor in forwarder.forward(fcmToken: token) }
        }
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
