import CleansiaCore
import Foundation

/// One cancellable registration cycle per current token. Token observers never wait through retry
/// pauses, so a rotation replaces an old cycle immediately. The coordinator clears this on every wipe.
@MainActor
final class LiveActivityRegistrationQueue {
    enum Scope: Hashable {
        case pushToStart
        case order(String)
    }

    private struct Entry {
        let token: String
        let orderNumber: String
        let revision: UInt64
        var task: Task<Void, Never>?
        var accepted = false
        var permanentlyRefused = false
        var retryAfter: Date?
    }

    private var registrar: LiveActivityRegistering
    private let now: () -> Date
    private let sleep: @MainActor (UInt64) async throws -> Void
    private var entries: [Scope: Entry] = [:]
    private var generation: UInt64 = 0
    private var revision: UInt64 = 0

    init(
        registrar: LiveActivityRegistering,
        now: @escaping () -> Date = Date.init,
        sleep: @escaping @MainActor (UInt64) async throws -> Void = { try await Task.sleep(nanoseconds: $0) }
    ) {
        self.registrar = registrar
        self.now = now
        self.sleep = sleep
    }

    func install(registrar: LiveActivityRegistering) {
        self.registrar = registrar
    }

    func submit(scope: Scope, orderNumber: String = "", token: String) {
        guard entries[scope]?.token != token else { return }
        entries[scope]?.task?.cancel()
        revision &+= 1
        entries[scope] = Entry(token: token, orderNumber: orderNumber, revision: revision)
        schedule(scope)
    }

    /// Replays only pending current tokens on authenticated foreground resume. Accepted tokens stay
    /// only to deduplicate repeated emissions; permanent refusals wait for a new token.
    func resume() {
        for scope in Array(entries.keys) {
            schedule(scope)
        }
    }

    func remove(orderId: String) {
        entries.removeValue(forKey: .order(orderId))?.task?.cancel()
    }

    func clear() {
        generation &+= 1
        for entry in entries.values {
            entry.task?.cancel()
        }
        entries.removeAll()
    }

    private func schedule(_ scope: Scope) {
        guard let entry = entries[scope], entry.task == nil, !entry.accepted, !entry.permanentlyRefused,
              entry.retryAfter.map({ $0 <= now() }) ?? true else { return }
        let session = generation
        entries[scope]?.task = Task { [weak self] in
            await self?.run(scope, revision: entry.revision, generation: session)
        }
    }

    private func matches(_ scope: Scope, revision: UInt64, generation: UInt64) -> Bool {
        self.generation == generation && entries[scope]?.revision == revision
    }

    private func run(_ scope: Scope, revision: UInt64, generation: UInt64) async {
        defer {
            if matches(scope, revision: revision, generation: generation) {
                entries[scope]?.task = nil
            }
        }
        for attempt in 0 ..< 3 {
            guard !Task.isCancelled, matches(scope, revision: revision, generation: generation),
                  let entry = entries[scope] else { return }
            let result: ApiResult<Void> = switch scope {
            case .pushToStart:
                await registrar.registerPushToStart(token: entry.token)
            case let .order(orderId):
                await registrar.register(orderId: orderId, orderNumber: entry.orderNumber, token: entry.token)
            }
            guard !Task.isCancelled, matches(scope, revision: revision, generation: generation) else { return }
            guard shouldRetry(result, scope: scope) else { return }
            guard attempt < 2 else { return }
            do {
                try await sleep(UInt64(2 << attempt) * 1_000_000_000)
            } catch {
                return
            }
        }
    }

    private func shouldRetry(_ result: ApiResult<Void>, scope: Scope) -> Bool {
        guard case let .failure(error) = result else {
            entries[scope]?.accepted = true
            return false
        }
        if error.isCancellation { return false }
        if error.httpStatus == 429 {
            entries[scope]?.retryAfter = now().addingTimeInterval(30)
            return false
        }
        if let status = error.httpStatus, status > 0, !(500 ... 599).contains(status) {
            entries[scope]?.permanentlyRefused = true
            return false
        }
        return true
    }
}
