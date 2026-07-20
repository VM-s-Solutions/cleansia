import CleansiaCustomerApi
import Foundation

enum BookingSuccessStepState {
    case done
    case active
    case pending
}

enum BookingSuccessTimelineStep: CaseIterable {
    case received
    case assigning
    case confirmed
    case cleaningDay

    var title: String {
        switch self {
        case .received: L10n.Booking.successStepReceivedTitle
        case .assigning: L10n.Booking.successStepAssigningTitle
        case .confirmed: L10n.Booking.successStepConfirmedTitle
        case .cleaningDay: L10n.Booking.successStepCleaningDayTitle
        }
    }

    var subtitle: String {
        switch self {
        case .received: L10n.Booking.successStepReceivedSubtitle
        case .assigning: L10n.Booking.successStepAssigningSubtitle
        case .confirmed: L10n.Booking.successStepConfirmedSubtitle
        case .cleaningDay: L10n.Booking.successStepCleaningDaySubtitle
        }
    }
}

struct BookingSuccessTimelineEntry: Equatable {
    let step: BookingSuccessTimelineStep
    let state: BookingSuccessStepState
}

enum BookingSuccessTimeline {
    /// `computeTimelineSteps` (`BookingSuccessScreen.kt`) parity. No order
    /// loaded yet → the "just placed" fallback (received done, assigning
    /// active) — what the user expects right after submission.
    static func entries(status: OrderStatus?, cleanerAssigned: Bool) -> [BookingSuccessTimelineEntry] {
        let assigning: BookingSuccessStepState = switch status {
        case nil: .active
        case ._0, ._1: cleanerAssigned ? .done : .active
        case ._2, ._3, ._4, ._5, ._6: .done
        }
        let confirmed: BookingSuccessStepState = switch status {
        case nil, ._0, ._1, ._6: .pending
        case ._2, ._3: .active
        case ._4, ._5: .done
        }
        let cleaningDay: BookingSuccessStepState = switch status {
        case ._4: .active
        case ._5: .done
        default: .pending
        }
        return [
            BookingSuccessTimelineEntry(step: .received, state: .done),
            BookingSuccessTimelineEntry(step: .assigning, state: assigning),
            BookingSuccessTimelineEntry(step: .confirmed, state: confirmed),
            BookingSuccessTimelineEntry(step: .cleaningDay, state: cleaningDay)
        ]
    }
}
