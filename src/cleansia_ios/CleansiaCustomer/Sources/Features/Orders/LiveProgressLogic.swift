import CleansiaCustomerApi
import Foundation

enum LiveProgressStep: Int, CaseIterable {
    case booked = 0
    case accepted = 1
    case onTheWay = 2
    case started = 3
    case finished = 4

    var label: String {
        switch self {
        case .booked: L10n.OrderDetail.stepBooked
        case .accepted: L10n.OrderDetail.stepAccepted
        case .onTheWay: L10n.OrderDetail.stepOnTheWay
        case .started: L10n.OrderDetail.stepStarted
        case .finished: L10n.OrderDetail.stepFinished
        }
    }
}

enum LiveProgress {
    struct EstimateProgress: Equatable {
        let fraction: Double
        let remainingMinutes: Int
        let overrunMinutes: Int
    }

    /// Active step index for the five-dot indicator, mirroring `StepIndicator`
    /// in `OrderDetailHeader.kt`. New/Pending → Booked, Confirmed →
    /// Accepted, OnTheWay → On the way, InProgress → Started, Completed →
    /// Finished; Cancelled (or unknown) → none.
    static func activeStep(for status: OrderStatus?) -> LiveProgressStep? {
        switch status {
        case ._0, ._1: .booked
        case ._2: .accepted
        case ._3: .onTheWay
        case ._4: .started
        case ._5: .finished
        default: nil
        }
    }

    static func inProgressFraction(
        history: [OrderStatusTrackDto]?,
        estimatedMinutes: Int,
        now: Date
    ) -> Double? {
        estimateProgress(history: history, estimatedMinutes: estimatedMinutes, now: now)?.fraction
    }

    static func estimateProgress(
        history: [OrderStatusTrackDto]?,
        estimatedMinutes: Int,
        now: Date
    ) -> EstimateProgress? {
        guard estimatedMinutes > 0 else { return nil }
        guard let startedAt = history?
            .filter({ $0.statusEnum == ._4 })
            .compactMap(\.createdOn)
            .max(), startedAt <= now
        else { return nil }
        let totalSeconds = Double(estimatedMinutes) * 60
        let elapsed = now.timeIntervalSince(startedAt)
        return EstimateProgress(
            fraction: min(1, elapsed / totalSeconds),
            remainingMinutes: max(0, Int(ceil((totalSeconds - elapsed) / 60))),
            overrunMinutes: max(0, Int(ceil((elapsed - totalSeconds) / 60)))
        )
    }
}
