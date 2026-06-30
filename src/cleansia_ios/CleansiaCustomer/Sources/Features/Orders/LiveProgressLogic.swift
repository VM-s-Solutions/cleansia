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
    /// Active step index for the five-dot indicator, mirroring
    /// `LiveProgressHero.kt:296-303`. New/Pending → Booked, Confirmed →
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

    /// Whether the hero shows the live "in-progress" treatment (mascot +
    /// progress bar) — only for active statuses (`OrderDetailScreen.kt:638-640`).
    static func usesLiveHero(_ status: OrderStatus?) -> Bool {
        OrderStatusGroup.isActive(status)
    }

    /// Progress fraction (0…0.97) since the InProgress entry was recorded vs the
    /// estimated duration. Returns nil when either anchor is missing — never a
    /// guess (`LiveProgressHero.kt:258-276`).
    static func inProgressFraction(
        history: [OrderStatusTrackDto]?,
        estimatedMinutes: Int,
        now: Date
    ) -> Double? {
        guard estimatedMinutes > 0 else { return nil }
        guard let startedAt = history?
            .first(where: { $0.statusEnum == ._4 })?
            .createdOn
        else { return nil }
        let totalSeconds = Double(estimatedMinutes) * 60
        guard totalSeconds > 0 else { return nil }
        let elapsed = max(0, now.timeIntervalSince(startedAt))
        return min(0.97, max(0, elapsed / totalSeconds))
    }
}
