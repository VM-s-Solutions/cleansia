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
        // Ground truth: is the push entitlement actually in the signed binary?
        PushDiagnostics.logApsEnvironment()
        // Dev builds fetch their token via SANDBOX APNs — a separate channel from
        // the production one other apps use. Prove reachability directly.
        ApnsReachabilityProbe.run()
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
        // A failure here (typically "no valid 'aps-environment' entitlement
        // string found") is the definitive sign that Push is not provisioned
        // on the App ID / profile — not a code problem.
        PushLog.log.error("APNs registration FAILED: \(error.localizedDescription, privacy: .public)")
    }

    func messaging(_: Messaging, didReceiveRegistrationToken fcmToken: String?) {
        let tokenLen = fcmToken?.count ?? 0
        PushLog.log.notice("messaging delegate fired (fcm tokenLen=\(tokenLen, privacy: .public))")
        guard let registrar else {
            // Recovered later by the requestFcmToken pull, but never drop silently.
            PushLog.log.error("messaging delegate DROPPED the token: registrar not attached yet")
            return
        }
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
    func requestFcmToken(retriesLeft: Int = 6) {
        guard firebaseConfigured else {
            PushLog.log.error("FCM token requested but Firebase is not configured")
            return
        }
        Messaging.messaging().token { [weak self] token, error in
            if let token, !token.isEmpty {
                PushLog.log.notice("FCM token fetched (len=\(token.count, privacy: .public))")
                guard let self, let registrar else { return }
                let forwarder = PushTokenForwarder(
                    registrar: registrar,
                    isFirebaseConfigured: { [weak self] in self?.firebaseConfigured ?? false }
                )
                Task { @MainActor in forwarder.forward(fcmToken: token) }
                return
            }
            guard retriesLeft > 0 else {
                PushLog.log.error(
                    "FCM token STILL unavailable: the OS accepted the registration request but never completed the APNs handshake (no token, no failure). Provisioning was verified at launch, so this is the SANDBOX APNs connection (dev builds use courier.sandbox.push.apple.com — separate from the production channel other apps use). Check the 'APNs probe' verdicts above; try cellular with Wi-Fi OFF; toggle Airplane Mode; on the Mac, Console.app → this device → filter 'apsd' shows the courier errors. Last error: \(String(describing: error), privacy: .public)"
                )
                return
            }
            PushLog.log.notice("FCM token not ready (APNs pending); retrying, \(retriesLeft, privacy: .public) left")
            Task { @MainActor [weak self] in
                try? await Task.sleep(nanoseconds: 4_000_000_000)
                self?.requestFcmToken(retriesLeft: retriesLeft - 1)
            }
        }
    }
}
