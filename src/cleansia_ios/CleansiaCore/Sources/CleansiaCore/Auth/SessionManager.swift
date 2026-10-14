import Combine
import Foundation

public enum ForcedSignOutReason: Equatable, Sendable {
    case sessionExpired
    case compromised
    case userInitiated
}

@MainActor
public final class SessionManager: ObservableObject {
    @Published public private(set) var lastForcedSignOut: ForcedSignOutReason?

    private var continuations: [UUID: AsyncStream<ForcedSignOutReason>.Continuation] = [:]

    public init() {}

    public var forcedSignOutStream: AsyncStream<ForcedSignOutReason> {
        AsyncStream { continuation in
            let id = UUID()
            continuations[id] = continuation
            continuation.onTermination = { [weak self] _ in
                Task { @MainActor in self?.continuations[id] = nil }
            }
        }
    }

    public func emitForcedSignOut(_ reason: ForcedSignOutReason) {
        lastForcedSignOut = reason
        for continuation in continuations.values {
            continuation.yield(reason)
        }
    }
}

extension SessionManager {
    var lastReasonForTest: ForcedSignOutReason? {
        lastForcedSignOut
    }
}
