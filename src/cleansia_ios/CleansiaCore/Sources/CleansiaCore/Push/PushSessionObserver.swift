import Combine
import Foundation

public final class PushSessionObserver: @unchecked Sendable {
    private let registrar: PushTokenRegistrar
    private var cancellable: AnyCancellable?

    public init(registrar: PushTokenRegistrar) {
        self.registrar = registrar
    }

    public func attach(
        hasSession: AnyPublisher<Bool, Never>,
        apnsToken: AnyPublisher<String?, Never>
    ) {
        // A live session with no APNs token yet registers token-less ("" is the
        // backend's canonical token-less form): the device row must exist for the
        // Devices page and remote revocation even when push permission or APNs
        // provisioning never yields a token. A later real token re-registers and
        // upgrades the row; the backend never lets a token-less re-register wipe
        // a stored real token.
        cancellable = hasSession
            .combineLatest(apnsToken)
            .map { session, token in session ? (token ?? "") : nil }
            .removeDuplicates()
            .sink { [registrar] token in
                guard let token else { return }
                Task { await registrar.ensureRegistered(token: token) }
            }
    }
}
