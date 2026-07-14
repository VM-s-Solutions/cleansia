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
        guard isFirebaseConfigured(), let token = fcmToken, !token.isEmpty else { return }
        registrar.reportRegistered(token: token)
    }
}
