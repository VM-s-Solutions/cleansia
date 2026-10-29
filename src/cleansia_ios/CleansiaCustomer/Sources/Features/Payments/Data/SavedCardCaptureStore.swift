import CleansiaCore
import Combine
import Foundation

struct SavedCardCaptureScope: Hashable {
    let countryId: String?
    let currencyCode: String?

    init(countryId: String?, currencyCode: String?) {
        self.countryId = countryId
        let code = currencyCode?.trimmingCharacters(in: .whitespacesAndNewlines).uppercased()
        self.currencyCode = code?.isEmpty == false ? code : nil
    }
}

struct SavedCardCaptureAttempt: Equatable {
    let id = UUID()
    let generation: UInt
}

struct SavedCardCapture: Equatable {
    enum Stage: Equatable {
        case starting
        case awaitingSheet(SavedCardSetup)
        case presentingSheet(SavedCardSetup)
        case pending(String)
        case verifying(String)

        var isBusy: Bool {
            switch self {
            case .starting, .awaitingSheet, .presentingSheet, .verifying: true
            case .pending: false
            }
        }

        var savedCardId: String? {
            switch self {
            case let .pending(id), let .verifying(id): id
            case .starting, .awaitingSheet, .presentingSheet: nil
            }
        }
    }

    let attempt: SavedCardCaptureAttempt
    var stage: Stage
}

struct SavedCardSheetRequest {
    let attempt: SavedCardCaptureAttempt
    let presentation: PaymentSheetPresentation
}

@MainActor
final class SavedCardCaptureStore: SessionScopedCache {
    enum Event {
        case observed(SavedCardCaptureScope, [SavedCard])
        case confirmed(SavedCardCaptureScope, [SavedCard])
        case failure(SavedCardCaptureScope, ApiError)
    }

    static let cardReads = 8
    let changes = PassthroughSubject<Void, Never>()
    let events = PassthroughSubject<Event, Never>()
    private(set) var generation: UInt = 0
    private(set) var activeScope: SavedCardCaptureScope?
    private var captures: [SavedCardCaptureScope: SavedCardCapture] = [:]
    private var verificationJobs: [SavedCardCaptureScope: (id: UUID, task: Task<Void, Never>)] = [:]
    private var marketObserver: AnyCancellable?

    init(market: AnyPublisher<MarketState, Never>? = nil) {
        marketObserver = market?.sink { [weak self] state in
            self?.activate(SavedCardCaptureScope(
                countryId: state.selected?.countryId,
                currencyCode: state.selected?.currencyCode
            ))
        }
    }

    func capture(for scope: SavedCardCaptureScope) -> SavedCardCapture? {
        activeScope == scope ? captures[scope] : nil
    }

    func activate(_ scope: SavedCardCaptureScope) {
        guard activeScope != scope else { return }
        for (other, job) in verificationJobs where other != scope {
            job.task.cancel()
            verificationJobs.removeValue(forKey: other)
            if var capture = captures[other], let id = capture.stage.savedCardId {
                capture.stage = .pending(id)
                captures[other] = capture
            }
        }
        activeScope = scope
        changes.send()
    }

    func begin(in scope: SavedCardCaptureScope) -> SavedCardCaptureAttempt? {
        guard activeScope == scope, captures[scope] == nil else { return nil }
        let attempt = SavedCardCaptureAttempt(generation: generation)
        captures[scope] = SavedCardCapture(attempt: attempt, stage: .starting)
        changes.send()
        return attempt
    }

    func isCurrent(_ attempt: SavedCardCaptureAttempt, in scope: SavedCardCaptureScope) -> Bool {
        attempt.generation == generation && captures[scope]?.attempt == attempt
    }

    func install(_ setup: SavedCardSetup, attempt: SavedCardCaptureAttempt, in scope: SavedCardCaptureScope) -> Bool {
        guard isCurrent(attempt, in: scope) else { return false }
        captures[scope]?.stage = .awaitingSheet(setup)
        changes.send()
        return true
    }

    func takeSheet(attempt: SavedCardCaptureAttempt, in scope: SavedCardCaptureScope) -> SavedCardSetup? {
        guard activeScope == scope, isCurrent(attempt, in: scope),
              case let .awaitingSheet(setup) = captures[scope]?.stage else { return nil }
        captures[scope]?.stage = .presentingSheet(setup)
        changes.send()
        return setup
    }

    func markPending(attempt: SavedCardCaptureAttempt, in scope: SavedCardCaptureScope) -> Bool {
        guard isCurrent(attempt, in: scope),
              case let .presentingSheet(setup) = captures[scope]?.stage else { return false }
        captures[scope]?.stage = .pending(setup.savedCardId)
        changes.send()
        return true
    }

    func discard(in scope: SavedCardCaptureScope, attempt: SavedCardCaptureAttempt? = nil) {
        if let attempt, !isCurrent(attempt, in: scope) { return }
        verificationJobs.removeValue(forKey: scope)?.task.cancel()
        captures.removeValue(forKey: scope)
        changes.send()
    }

    @discardableResult
    func reconcile(_ cards: [SavedCard], in scope: SavedCardCaptureScope) -> Bool {
        guard activeScope == scope, let capture = captures[scope],
              let id = capture.stage.savedCardId,
              cards.contains(where: { card in
                  card.id == id && (scope.currencyCode == nil || card.currencyCode.uppercased() == scope.currencyCode)
              }) else { return false }
        discard(in: scope, attempt: capture.attempt)
        events.send(.confirmed(scope, cards))
        return true
    }

    func verify(
        in scope: SavedCardCaptureScope,
        client: SavedCardClient,
        pause: @escaping () async -> Void
    ) async {
        guard activeScope == scope, let capture = captures[scope], let id = capture.stage.savedCardId else { return }
        if let job = verificationJobs[scope] {
            await job.task.value
            return
        }
        let jobId = UUID()
        captures[scope]?.stage = .verifying(id)
        changes.send()
        let task = Task { [weak self] in
            guard let self else { return }
            await runVerification(
                in: scope, attempt: capture.attempt, jobId: jobId, client: client, pause: pause
            )
        }
        verificationJobs[scope] = (jobId, task)
        await task.value
    }

    private func runVerification(
        in scope: SavedCardCaptureScope,
        attempt: SavedCardCaptureAttempt,
        jobId: UUID,
        client: SavedCardClient,
        pause: @escaping () async -> Void
    ) async {
        defer {
            if verificationJobs[scope]?.id == jobId {
                verificationJobs.removeValue(forKey: scope)
                if isCurrent(attempt, in: scope), let id = captures[scope]?.stage.savedCardId {
                    captures[scope]?.stage = .pending(id)
                    changes.send()
                }
            }
        }
        for read in 0 ..< Self.cardReads {
            if read > 0 { await pause() }
            guard !Task.isCancelled, activeScope == scope, isCurrent(attempt, in: scope) else { return }
            let result = await client.myCards()
            guard !Task.isCancelled, activeScope == scope, isCurrent(attempt, in: scope) else { return }
            switch result {
            case let .success(cards):
                events.send(.observed(scope, cards))
                if reconcile(cards, in: scope) { return }
            case let .failure(error):
                events.send(.failure(scope, error))
                return
            }
        }
    }

    func clear() async {
        generation &+= 1
        verificationJobs.values.forEach { $0.task.cancel() }
        verificationJobs.removeAll()
        captures.removeAll()
        changes.send()
    }
}
