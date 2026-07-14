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
            .handleEvents(receiveOutput: { session, token in
                // Log every raw emission so the exact gate state is visible.
                PushLog.log.notice(
                    "register gate: session=\(session, privacy: .public) token=\(token != nil ? "present" : "MISSING", privacy: .public)"
                )
            })
            .map { session, token in session ? token : nil }
            .removeDuplicates()
            .sink { [registrar] token in
                guard let token else { return }
                PushLog.log.notice("register gate OPEN -> ensureRegistered")
                Task { await registrar.ensureRegistered(token: token) }
            }
    }
}
