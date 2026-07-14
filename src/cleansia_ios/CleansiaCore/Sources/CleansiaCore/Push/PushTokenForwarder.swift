import Foundation

@MainActor
public struct PushTokenForwarder {
    private let registrar: any PushRegistrar
    private let isFirebaseConfigured: () -> Bool

    public init(
        registrar: any PushRegistrar,
        isFirebaseConfigured: @escaping () -> Bool
    ) {
        self.registrar = registrar
        self.isFirebaseConfigured = isFirebaseConfigured
    }

    public func forward(fcmToken: String?) {
        // Every drop is logged — a silent return here hides the break point.
        guard isFirebaseConfigured() else {
            PushLog.log.error("forward DROPPED: Firebase not configured")
            return
        }
        guard let token = fcmToken, !token.isEmpty else {
            PushLog.log.error("forward DROPPED: fcm token nil/empty")
            return
        }
        PushLog.log.notice("forward: fcm token (len=\(token.count, privacy: .public)) -> registrar")
        registrar.reportRegistered(token: token)
    }
}
