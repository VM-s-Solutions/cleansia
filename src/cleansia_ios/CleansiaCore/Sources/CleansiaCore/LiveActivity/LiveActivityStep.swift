import Foundation

/// The content-state `status` strings the backend writes (ADR-0029 D4). Decoding stays string-based on the
/// wire: a value this build does not know is not a decode failure, it is an unknown position.
public enum LiveActivityStatus: String, Sendable, CaseIterable {
    case onTheWay
    case inProgress
    case completed
    case cancelled
}

/// One leg's position relative to where the order actually is.
public enum LiveActivityLeg: Sendable, Equatable {
    case complete
    case current
    case upcoming
}

/// The four legs the lock-screen card walks. Deliberately the same journey the in-app order screen tells,
/// so a glance at the lock screen and a look at the app never disagree.
public enum LiveActivityStep: Int, Sendable, CaseIterable {
    case confirmed
    case onTheWay
    case cleaning
    case done

    public static let count = allCases.count

    public var number: Int {
        rawValue + 1
    }

    public var previous: LiveActivityStep? {
        LiveActivityStep(rawValue: rawValue - 1)
    }

    public var label: String {
        switch self {
        case .confirmed: LiveActivityL10n.Step.confirmed
        case .onTheWay: LiveActivityL10n.Step.onTheWay
        case .cleaning: LiveActivityL10n.Step.cleaning
        case .done: LiveActivityL10n.Step.done
        }
    }

    /// nil for `confirmed`: a card never opens before the cleaner is on the way, so that leg only ever
    /// renders as a passed marker.
    public var detail: String? {
        switch self {
        case .confirmed: nil
        case .onTheWay: LiveActivityL10n.Status.onTheWayDetail
        case .cleaning: LiveActivityL10n.Status.inProgressDetail
        case .done: LiveActivityL10n.Status.completedDetail
        }
    }

    public func leg(at position: LiveActivityStep) -> LiveActivityLeg {
        if rawValue < position.rawValue {
            .complete
        } else if rawValue == position.rawValue {
            .current
        } else {
            .upcoming
        }
    }
}

/// The line the card leads with. A card in service names the move it just made; one that is over names
/// its outcome.
public enum LiveActivityHeadline: Sendable, Equatable {
    case transition(from: LiveActivityStep, to: LiveActivityStep)
    case completed
    case cancelled
    case generic
}

/// What the card's clock — and the countdown that shares its instant — is timing. The two legs of a live
/// clean end at different things: the on-the-way leg ends when the cleaner ARRIVES, the cleaning leg when
/// the clean is done. One caption for both puts the arrival on the lock screen as the moment the clean ends.
public enum LiveActivityTimeCaption: Sendable, Equatable, CaseIterable {
    case arrival
    case finish

    public var label: String {
        switch self {
        case .arrival: LiveActivityL10n.arrival
        case .finish: LiveActivityL10n.finish
        }
    }
}

/// What the card renders for one content-state status.
public enum LiveActivityCard: Sendable, Equatable {
    case journey(LiveActivityStep)
    case cancelled
    case unknown

    public static func forStatus(_ status: String) -> LiveActivityCard {
        guard let known = LiveActivityStatus(rawValue: status) else { return .unknown }
        return switch known {
        case .onTheWay: .journey(.onTheWay)
        case .inProgress: .journey(.cleaning)
        case .completed: .journey(.done)
        case .cancelled: .cancelled
        }
    }

    public var headline: LiveActivityHeadline {
        switch self {
        case .journey(.done): .completed
        case let .journey(step): step.previous.map { .transition(from: $0, to: step) } ?? .generic
        case .cancelled: .cancelled
        case .unknown: .generic
        }
    }

    /// Where the stepper puts its marker; nil draws no stepper at all rather than an invented position.
    public var position: LiveActivityStep? {
        switch self {
        case let .journey(step): step
        case .cancelled, .unknown: nil
        }
    }

    /// What the card's clock promises, or nil where it has nothing left to time and draws no clock at all.
    /// An unknown status is still in service but cannot know its leg, so it claims the weaker of the two.
    public var timeCaption: LiveActivityTimeCaption? {
        switch self {
        case .journey(.onTheWay): .arrival
        case .journey(.done), .cancelled: nil
        case .journey, .unknown: .finish
        }
    }

    public var detail: String? {
        switch self {
        case let .journey(step): step.detail
        case .cancelled: LiveActivityL10n.Status.cancelledDetail
        case .unknown: nil
        }
    }
}
