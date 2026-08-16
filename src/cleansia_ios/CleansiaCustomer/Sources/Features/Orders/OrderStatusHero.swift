import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

/// What is happening to the booking, in a sentence — plus a subhead and, while the clean is running, an
/// elapsed-over-estimated bar.
///
/// It carries no date and no price. Both are already on screen: the date in `OrderDetailCompactHeader`
/// above, the price in `OrderHeroFactsStrip` below.
///
/// **It draws no container.** The gradient card it used to sit in fenced it off from the tracker bar it
/// is meant to read as one block with, and cost a band of whitespace above and below it. The sheet is
/// the surface. `OrderDetailHeader.kt` is the Android twin; keep the two in step.
struct OrderStatusHero: View {
    let order: CustomerOrderDetail

    private var status: OrderStatus? {
        order.status
    }

    private var cleanerName: String? {
        order.assignedEmployees.first?.fullName.flatMap { $0.isBlank ? nil : $0 }
    }

    var body: some View {
        if status != ._6 {
            hero
        }
    }

    private var hero: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                // The step counter shares the headline's line. On its own row it was a full-width band
                // with an empty left half, which read as a gap between the headline and the bar it
                // belongs to — and it is the bar's own label, so it belongs beside the phase.
                HStack(alignment: .lastTextBaseline, spacing: Spacing.xs) {
                    Text(headline)
                        .font(CleansiaTypography.titleLarge)
                        .fontWeight(.bold)
                        .foregroundColor(CleansiaColors.onSurface)
                    Spacer(minLength: Spacing.xs)
                    Text(stepCounter)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .fixedSize()
                }
                if let subhead {
                    Text(subhead)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)

            if status == ._4 {
                progressBar
            }
        }
        // No bottom padding: the headline sits directly on the tracker bar, the way the partner sheet's
        // timer text does. The gap that used to be here read as a break between two unrelated things
        // when they are one block — the phase, said in words and then drawn.
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var progressBar: some View {
        TimelineView(.periodic(from: .now, by: 30)) { context in
            if let fraction = LiveProgress.inProgressFraction(
                history: order.statusHistory,
                estimatedMinutes: order.estimatedMinutes,
                now: context.date
            ) {
                VStack(alignment: .leading, spacing: Spacing.xxs) {
                    ProgressView(value: fraction)
                        .tint(CleansiaColors.primary)
                    Text(L10n.OrderDetail.progressPercent(Int((fraction * 100).rounded())))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
        }
    }

    private var headline: String {
        switch status {
        case ._2:
            cleanerName.map(L10n.OrderDetail.headlineConfirmedNamed) ?? L10n.OrderDetail.headlineConfirmed
        case ._3:
            cleanerName.map(L10n.OrderDetail.headlineOnTheWayNamed) ?? L10n.OrderDetail.headlineOnTheWay
        case ._4:
            cleanerName.map(L10n.OrderDetail.headlineInProgressNamed) ?? L10n.OrderDetail.headlineInProgress
        case ._5:
            L10n.OrderDetail.headlineCompleted
        default:
            L10n.OrderDetail.headlineDefault
        }
    }

    /// Drawn here rather than by `OrderTrackerBar`, which is passed no label for exactly this reason.
    private var stepCounter: String {
        let step = OrderTrackerRule.state(
            step: LiveProgress.activeStep(for: status)?.rawValue ?? 0,
            cancelled: false,
            completed: status == ._5
        )
        guard case let .steps(_, stepNumber) = step else { return "" }
        return L10n.OrderDetail.trackerStepCounter(stepNumber, OrderTrackerRule.stepCount)
    }

    private var subhead: String? {
        switch status {
        case ._2: L10n.OrderDetail.subheadConfirmed
        case ._3: L10n.OrderDetail.subheadOnTheWay
        case ._4:
            order.estimatedMinutes > 0
                ? L10n.OrderDetail.subheadInProgressEta(order.estimatedMinutes)
                : L10n.OrderDetail.subheadInProgress
        default: nil
        }
    }
}
