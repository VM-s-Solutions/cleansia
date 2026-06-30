import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct LiveProgressHero: View {
    let order: OrderItem

    private var status: OrderStatus? {
        order.status
    }

    private var cleanerName: String? {
        order.assignedEmployees?.first?.fullName?.isBlank == false
            ? order.assignedEmployees?.first?.fullName
            : nil
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    OrderStatusPill(
                        label: OrderStatusPresentation.label(order.orderStatus),
                        color: OrderStatusPresentation.color(order.orderStatus)
                    )
                    Text(headline)
                        .font(CleansiaTypography.titleLarge)
                        .foregroundColor(CleansiaColors.onSurface)
                    if let subhead {
                        Text(subhead)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
                Spacer()
                Image(systemName: heroSymbol)
                    .font(.system(size: 44))
                    .foregroundColor(CleansiaColors.primary)
            }

            if status == ._4 {
                progressBar
            }

            StepIndicator(activeStep: LiveProgress.activeStep(for: status))
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(
            LinearGradient(
                colors: [CleansiaColors.primary.opacity(0.10), CleansiaColors.surface],
                startPoint: .top,
                endPoint: .bottom
            ),
            in: RoundedRectangle(cornerRadius: CornerRadius.large)
        )
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.large)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
    }

    private var progressBar: some View {
        TimelineView(.periodic(from: .now, by: 30)) { context in
            if let fraction = LiveProgress.inProgressFraction(
                history: order.statusHistory,
                estimatedMinutes: order.estimatedTime ?? 0,
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

    private var heroSymbol: String {
        switch status {
        case ._4: "sparkles"
        case ._2, ._3: "hand.wave.fill"
        default: "checkmark.seal.fill"
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
        default:
            L10n.OrderDetail.headlineDefault
        }
    }

    private var subhead: String? {
        switch status {
        case ._2: L10n.OrderDetail.subheadConfirmed
        case ._3: L10n.OrderDetail.subheadOnTheWay
        case ._4:
            (order.estimatedTime ?? 0) > 0
                ? L10n.OrderDetail.subheadInProgressEta(order.estimatedTime ?? 0)
                : L10n.OrderDetail.subheadInProgress
        default: nil
        }
    }
}

private struct StepIndicator: View {
    let activeStep: LiveProgressStep?

    private var activeIndex: Int {
        activeStep?.rawValue ?? -1
    }

    var body: some View {
        VStack(spacing: Spacing.xs) {
            HStack(spacing: Spacing.xxs) {
                ForEach(LiveProgressStep.allCases, id: \.self) { step in
                    Circle()
                        .fill(step.rawValue <= activeIndex ? CleansiaColors.primary : CleansiaColors.outlineVariant)
                        .frame(width: 10, height: 10)
                    if step != LiveProgressStep.allCases.last {
                        Rectangle()
                            .fill(step.rawValue < activeIndex ? CleansiaColors.primary : CleansiaColors.outlineVariant)
                            .frame(height: 2)
                            .frame(maxWidth: .infinity)
                    }
                }
            }
            HStack(spacing: 0) {
                ForEach(LiveProgressStep.allCases, id: \.self) { step in
                    Text(step.label)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(step.rawValue == activeIndex ? CleansiaColors.primary : CleansiaColors
                            .onSurfaceVariant)
                        .frame(maxWidth: .infinity)
                        .multilineTextAlignment(.center)
                }
            }
        }
    }
}
