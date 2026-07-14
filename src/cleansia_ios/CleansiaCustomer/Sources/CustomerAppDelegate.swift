import CleansiaCore
import FirebaseCore
import FirebaseMessaging
import UIKit
import UserNotifications

final class CustomerAppDelegate: NSObject, UIApplicationDelegate, UNUserNotificationCenterDelegate, MessagingDelegate {
    weak var registrar: (any PushRegistrar)?
    private(set) var firebaseConfigured = false

    func application(
        _ application: UIApplication,
        didFinishLaunchingWithOptions _: [UIApplication.LaunchOptionsKey: Any]? = nil
    ) -> Bool {
        // APNs registration MUST happen here, directly in didFinishLaunching
        // (Apple's documented pattern). Do NOT move it into an async flow:
        // iOS silently drops the call when it is deferred behind
        // requestAuthorization (no token, no failure callback) — proven
        // empirically on device and simulator. Registration needs no
        // notification permission; permission gates alert DISPLAY only and is
        // requested separately in startPush.
        application.registerForRemoteNotifications()
        if GoogleServicePlist.isPresent {
            FirebaseApp.configure()
            Messaging.messaging().delegate = self
            firebaseConfigured = true
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
        Messaging.messaging().apnsToken = deviceToken
    }

    func application(
        _: UIApplication,
        didFailToRegisterForRemoteNotificationsWithError error: Error
    ) {
        // Typically "no valid 'aps-environment' entitlement string found" —
        // a provisioning/signing gap, not a code problem.
        PushLog.log.error("APNs registration FAILED: \(error.localizedDescription, privacy: .public)")
    }

    func messaging(_: Messaging, didReceiveRegistrationToken fcmToken: String?) {
        // A nil registrar just means the SwiftUI .task has not attached it yet;
        // the requestFcmToken pull re-fetches the token right after it does.
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
    /// proactively, retrying while the APNs token settles after launch.
    func requestFcmToken(retriesLeft: Int = 6) {
        guard firebaseConfigured else { return }
        Messaging.messaging().token { [weak self] token, error in
            if let token, !token.isEmpty {
                guard let self, let registrar else { return }
                let forwarder = PushTokenForwarder(
                    registrar: registrar,
                    isFirebaseConfigured: { [weak self] in self?.firebaseConfigured ?? false }
                )
                Task { @MainActor in forwarder.forward(fcmToken: token) }
                return
            }
            guard retriesLeft > 0 else {
                PushLog.log.error("FCM token unavailable after retries: \(String(describing: error), privacy: .public)")
                return
            }
            Task { @MainActor [weak self] in
                try? await Task.sleep(nanoseconds: 4_000_000_000)
                self?.requestFcmToken(retriesLeft: retriesLeft - 1)
            }
        }
    }
}
