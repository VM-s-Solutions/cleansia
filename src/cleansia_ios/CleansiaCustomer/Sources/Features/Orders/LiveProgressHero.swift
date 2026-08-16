import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct LiveProgressHero: View {
    let order: CustomerOrderDetail

    private var status: OrderStatus? {
        order.status
    }

    private var cleanerName: String? {
        order.assignedEmployees.first?.fullName.flatMap { $0.isBlank ? nil : $0 }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(headline)
                    .font(CleansiaTypography.titleLarge)
                    .foregroundColor(CleansiaColors.onSurface)
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
