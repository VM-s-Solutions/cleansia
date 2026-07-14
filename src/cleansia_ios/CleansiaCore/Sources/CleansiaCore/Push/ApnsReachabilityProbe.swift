import Foundation
import Network

/// Probes TCP reachability of the APNs courier hosts and logs the verdicts on the
/// push channel.
///
/// Why: a development-signed build obtains its device token over a persistent
/// connection to the SANDBOX courier (`courier.sandbox.push.apple.com`, port 5223
/// with a 443 fallback) — a separate service from the production channel every
/// App Store app uses (TN2265). So "other apps receive pushes" does NOT prove the
/// sandbox channel works; networks and outages can break sandbox alone, and the
/// symptom is exactly ours: `registerForRemoteNotifications()` yields neither a
/// token nor a failure. These probes make the network answer directly.
public enum ApnsReachabilityProbe {
    /// Fire-and-forget; each probe logs `reachable` / `FAILED` / `no verdict` on
    /// the push channel. Call once at launch.
    public static func run() {
        probe(host: "1-courier.sandbox.push.apple.com", port: 5223, label: "sandbox:5223 (what THIS dev build needs)")
        probe(host: "1-courier.sandbox.push.apple.com", port: 443, label: "sandbox:443 (fallback)")
        probe(host: "1-courier.push.apple.com", port: 5223, label: "production:5223 (control — other apps' channel)")
    }

    /// One-shot settle guard so each probe logs exactly one verdict.
    private final class Outcome: @unchecked Sendable {
        private let lock = NSLock()
        private var settled = false

        func trySettle() -> Bool {
            lock.lock()
            defer { lock.unlock() }
            if settled { return false }
            settled = true
            return true
        }
    }

    private static func probe(host: String, port: UInt16, label: String) {
        guard let nwPort = NWEndpoint.Port(rawValue: port) else { return }
        let connection = NWConnection(host: NWEndpoint.Host(host), port: nwPort, using: .tcp)
        let outcome = Outcome()

        connection.stateUpdateHandler = { state in
            switch state {
            case .ready:
                if outcome.trySettle() {
                    PushLog.log.notice("APNs probe \(label, privacy: .public): TCP reachable")
                }
                connection.cancel()
            case let .failed(error):
                if outcome.trySettle() {
                    PushLog.log
                        .error(
                            "APNs probe \(label, privacy: .public): FAILED — \(String(describing: error), privacy: .public)"
                        )
                }
                connection.cancel()
            case let .waiting(error):
                // Not terminal (may still connect); log the hint but keep waiting.
                PushLog.log
                    .notice(
                        "APNs probe \(label, privacy: .public): waiting — \(String(describing: error), privacy: .public)"
                    )
            default:
                break
            }
        }
        connection.start(queue: .global(qos: .utility))

        DispatchQueue.global(qos: .utility).asyncAfter(deadline: .now() + 8) {
            if outcome.trySettle() {
                PushLog.log
                    .error(
                        "APNs probe \(label, privacy: .public): no verdict within 8s — connection neither established nor refused (silently filtered/dropped?)"
                    )
            }
            connection.cancel()
        }
    }
}
