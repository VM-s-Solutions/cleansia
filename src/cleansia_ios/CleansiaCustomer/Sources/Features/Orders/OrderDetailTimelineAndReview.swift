import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct OrderTimelineCard: View {
    let history: [OrderStatusTrackDto]

    private var sorted: [OrderStatusTrackDto] {
        history.sorted { lhs, rhs in
            switch (lhs.createdOn, rhs.createdOn) {
            case let (left?, right?): left < right
            case (nil, _?): false
            case (_?, nil): true
            case (nil, nil): false
            }
        }
    }

    var body: some View {
        OrderCardSurface {
            OrderSectionHeaderRow(title: L10n.OrderDetail.timeline)
            ForEach(Array(sorted.enumerated()), id: \.offset) { index, entry in
                TimelineRow(entry: entry, isLast: index == sorted.count - 1)
            }
        }
    }
}

private struct TimelineRow: View {
    @Environment(\.locale) private var locale
    let entry: OrderStatusTrackDto
    let isLast: Bool

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            VStack(spacing: 0) {
                Circle()
                    .fill(OrderStatusPresentation.color(entry.status))
                    .frame(width: 12, height: 12)
                if !isLast {
                    Rectangle()
                        .fill(CleansiaColors.outlineVariant)
                        .frame(width: 2, height: 28)
                }
            }
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(OrderStatusPresentation.label(entry.status))
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                Text(OrdersFormat.dateTime(entry.createdOn, locale: locale))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            Spacer()
        }
        .padding(.bottom, isLast ? 0 : Spacing.xxs)
    }
}

struct OrderReviewCard: View {
    let review: OrderReviewDto?
    let onLeaveReview: () -> Void

    var body: some View {
        OrderCardSurface {
            OrderSectionHeaderRow(title: L10n.OrderDetail.yourReview)
            if let review {
                StarsRow(rating: review.rating ?? 0)
                if let comment = review.comment, !comment.isBlank {
                    Text(verbatim: "“\(comment)”")
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                CleansiaOutlinedButton(L10n.OrderDetail.editReview, action: onLeaveReview)
            } else {
                CleansiaPrimaryButton(L10n.OrderDetail.leaveReview, action: onLeaveReview)
            }
        }
    }
}

private struct StarsRow: View {
    let rating: Int

    var body: some View {
        HStack(spacing: Spacing.xxs) {
            ForEach(1 ... 5, id: \.self) { star in
                Image(systemName: star <= rating ? "star.fill" : "star")
                    .font(.system(size: 20))
                    .foregroundColor(star <= rating ? CleansiaColors.warningStar : CleansiaColors.outlineVariant)
            }
        }
    }
}

struct OrderReceiptCard: View {
    let order: OrderItem
    let isDownloading: Bool
    let onDownload: () -> Void

    private var hasReceipt: Bool {
        !(order.receiptNumber?.isBlank ?? true)
    }

    var body: some View {
        OrderCardSurface {
            HStack(spacing: Spacing.s) {
                Image(systemName: "doc.text")
                    .font(.system(size: 20))
                    .foregroundColor(CleansiaColors.primary)
                VStack(alignment: .leading, spacing: 0) {
                    Text(L10n.OrderDetail.downloadReceipt)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    if let number = order.receiptNumber, !number.isBlank {
                        Text(number)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
                Spacer()
                CleansiaPrimaryButton(
                    L10n.OrderDetail.downloadReceipt,
                    size: .small,
                    loading: isDownloading,
                    enabled: hasReceipt && !isDownloading,
                    action: onDownload
                )
                .fixedSize()
            }
            if !hasReceipt {
                Text(L10n.OrderDetail.receiptNotReady)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
        }
    }
}
