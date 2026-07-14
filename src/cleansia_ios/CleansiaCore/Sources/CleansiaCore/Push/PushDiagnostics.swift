import Foundation

/// Runtime inspection of whether Push is actually provisioned for the RUNNING
/// build — the ground truth that Xcode's project settings only imply.
///
/// Reads the `aps-environment` entitlement from the app's embedded provisioning
/// profile. A device build always ships `embedded.mobileprovision`; because
/// `aps-environment` is a capability-gated (non-wildcard) entitlement, its
/// presence in the profile means the App ID has Push Notifications enabled and
/// the profile authorizes it. Its absence is the usual cause of FCM 505 (iOS
/// issues no APNs token, and often no failure callback, on a real device).
public enum PushDiagnostics {
    /// `"development"` / `"production"` when Push is provisioned; `nil` when the
    /// profile lacks `aps-environment` or there is no embedded profile (e.g. the
    /// Simulator, which is never push-provisioned).
    public static func apsEnvironment() -> String? {
        guard let url = Bundle.main.url(forResource: "embedded", withExtension: "mobileprovision"),
              let data = try? Data(contentsOf: url),
              // The file is a CMS blob wrapping a plain-text XML plist; slice it out.
              let raw = String(data: data, encoding: .isoLatin1),
              let start = raw.range(of: "<?xml"),
              let end = raw.range(of: "</plist>")
        else {
            return nil
        }
        let plistText = String(raw[start.lowerBound ..< end.upperBound])
        guard let plistData = plistText.data(using: .isoLatin1),
              let plist = try? PropertyListSerialization.propertyList(from: plistData, format: nil) as? [String: Any],
              let entitlements = plist["Entitlements"] as? [String: Any]
        else {
            return nil
        }
        return entitlements["aps-environment"] as? String
    }

    /// Logs the running build's push provisioning on the push channel, with an
    /// actionable message when it is missing. Call once at launch.
    public static func logApsEnvironment() {
        if let env = apsEnvironment() {
            PushLog.log
                .notice("Push IS provisioned — aps-environment in the embedded profile = \(env, privacy: .public)")
        } else {
            PushLog.log.error(
                "Push is NOT provisioned for this build — the embedded provisioning profile has no aps-environment (or there is no profile). This is why iOS issues no APNs token AND no failure on a device. Fix: in Xcode → Signing & Capabilities, select your paid Team, add the Push Notifications capability (registers it on the App ID), then clean-rebuild on the device."
            )
        }
    }
}
