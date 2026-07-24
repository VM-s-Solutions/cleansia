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
    static func forOrder(_ order: OrderItem) -> EtaWindow? {
        guard let scheduledStart = order.cleaningDateTime else { return nil }
        let duration = TimeInterval(max(order.estimatedTime ?? 0, 1) * 60)
        let scheduledEnd = scheduledStart.addingTimeInterval(duration)
        guard let status = order.status,
              let phaseStart = LiveProgress.enteredAt(status, history: order.statusHistory)
        else {
            return EtaWindow(
                scheduledStart: scheduledStart,
                scheduledEnd: scheduledEnd,
                phaseStart: nil,
                phaseEnd: nil
            )
        }
        return EtaWindow(
            scheduledStart: scheduledStart,
            scheduledEnd: scheduledEnd,
            phaseStart: phaseStart,
            phaseEnd: phaseEnd(status: status, phaseStart: phaseStart, arrivalBy: scheduledStart, duration: duration)
        )
    }

    /// Mirrors `LiveActivityPayloadFactory.PhaseWindow` over the same three cards
    /// (`OrderStatusGroup.liveActivityStatus`): the on-the-way card counts to the expected ARRIVAL, the
    /// in-progress card to the estimate re-anchored on the real start. The app and the backend push write
    /// the same content-state, so they must describe the same window or the number jumps depending on
    /// which writer landed last.
    private static func phaseEnd(
        status: OrderStatus,
        phaseStart: Date,
        arrivalBy: Date,
        duration: TimeInterval
    ) -> Date? {
        switch status {
        case ._2, ._3: max(arrivalBy, phaseStart.addingTimeInterval(minPhaseWindow))
        case ._4: phaseStart.addingTimeInterval(max(duration, minPhaseWindow))
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
