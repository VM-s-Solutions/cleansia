import CleansiaCustomerApi
import Foundation

extension EtaWindow {
    /// A window that is already elapsed renders as a permanently-clamped 00:00, so every phase gets at
    /// least this much runway. Mirrors `LiveActivityPayloadFactory.MinPhaseWindow`.
    private static let minPhaseWindow: TimeInterval = 10 * 60

    /// The window the Live Activity ETA counts on for an order: the booked appointment plus the ACTUAL
    /// phase window read off the status history, so a cleaner running late still leaves an end that is
    /// ahead of "now" instead of one that has already passed.
    /// nil when the order carries no appointment time — there is nothing to count.
    static func forOrder(_ order: CustomerOrderDetail) -> EtaWindow? {
        guard let scheduledStart = order.cleaningDateTime else { return nil }
        let duration = TimeInterval(max(order.estimatedMinutes, 1) * 60)
        let phase = phase(for: order, arrivalBy: scheduledStart, duration: duration)

        return EtaWindow(
            scheduledStart: scheduledStart,
            scheduledEnd: scheduledStart.addingTimeInterval(duration),
            phaseStart: phase?.start,
            phaseEnd: phase?.end
        )
    }

    /// Mirrors `LiveActivityPayloadFactory.PhaseWindow` over the same two in-service cards
    /// (`OrderStatusGroup.liveActivityStatus`): the on-the-way card counts to the expected ARRIVAL, the
    /// in-progress card to the estimate re-anchored on the real start. The app and the backend push write
    /// the same content-state, so they must describe the same window or the number jumps depending on
    /// which writer landed last. Every other status has no phase to time, and both ends stay nil together —
    /// a start without an end anchors a countdown at a moment that was never a departure.
    private static func phase(
        for order: CustomerOrderDetail,
        arrivalBy: Date,
        duration: TimeInterval
    ) -> (start: Date, end: Date)? {
        guard let status = order.status,
              let start = LiveProgress.enteredAt(status, history: order.statusHistory)
        else { return nil }

        return switch status {
        case ._3: (start, max(arrivalBy, start.addingTimeInterval(minPhaseWindow)))
        case ._4: (start, start.addingTimeInterval(max(duration, minPhaseWindow)))
        default: nil
        }
    }
}

extension LiveProgress {
    /// When the order actually entered a status, per its history — the anchor the live ETA counts from.
    static func enteredAt(_ status: OrderStatus, history: [OrderStatusTrackDto]?) -> Date? {
        history?.first { $0.statusEnum == status }?.createdOn
    }
}
