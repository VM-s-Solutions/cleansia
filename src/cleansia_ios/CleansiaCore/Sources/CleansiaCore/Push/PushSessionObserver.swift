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
        cancellable = hasSession
            .combineLatest(apnsToken)
            .map { session, token in session ? token : nil }
            .removeDuplicates()
            .sink { [registrar] token in
                guard let token else {
                    PushLog.log.notice("register skipped: session invalid or no token yet")
                    return
                }
                PushLog.log.notice("register: session valid + token present -> ensureRegistered")
                Task { await registrar.ensureRegistered(token: token) }
            }
    }
}
