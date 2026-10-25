import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct OrderTimelineCard: View {
    let history: [OrderStatusTrackDto]

    struct Row {
        let entry: OrderStatusTrackDto
        let isOfferedAgain: Bool
    }

    /// Each status once, at the moment it was reached. A New after a Confirmed is read as the walk-back,
    /// that edge's only writer; a second writer would make the label untrue.
    /// → /domain/order-lifecycle#currentstatus-is-a-denormalisation-with-one-writer
    static func rows(_ history: [OrderStatusTrackDto]) -> [Row] {
        let sorted = history.sorted { lhs, rhs in
            switch (lhs.createdOn, rhs.createdOn) {
            case let (left?, right?): left < right
            case (nil, _?): false
            case (_?, nil): true
            case (nil, nil): false
            }
        }
        var rows: [Row] = []
        for entry in sorted {
            let previous = rows.last?.entry.status
            if let previous, previous.value == entry.status?.value {
                continue
            }
            let isOfferedAgain = entry.status?.toOrderStatus() == ._0 && previous?.toOrderStatus() == ._2
            rows.append(Row(entry: entry, isOfferedAgain: isOfferedAgain))
        }
        return rows
    }

    var body: some View {
        let rows = Self.rows(history)
        OrderCardSurface {
            OrderSectionHeaderRow(title: L10n.OrderDetail.timeline)
            ForEach(Array(rows.enumerated()), id: \.offset) { index, row in
                TimelineRow(entry: row.entry, isOfferedAgain: row.isOfferedAgain, isLast: index == rows.count - 1)
            }
        }
    }
}

private struct TimelineRow: View {
    @Environment(\.locale) private var locale
    let entry: OrderStatusTrackDto
    let isOfferedAgain: Bool
    let isLast: Bool

    private var label: String {
        isOfferedAgain ? L10n.OrderDetail.timelineOfferedAgain : OrderStatusPresentation.label(entry.status)
    }

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            VStack(spacing: 0) {
                // The most recent entry IS the order's current status, so it gets the bullseye and
                // everything above it the muted check — the same past/current split the partner sheet
                // draws. Nothing here is a future step: this card lists what has already happened.
                OrderTimelinePoint(
                    segment: isLast ? .current : .past,
                    tint: OrderStatusPresentation.color(entry.status)
                )
                if !isLast {
                    Rectangle()
                        .fill(CleansiaColors.outlineVariant)
                        .frame(width: 2, height: 28)
                }
            }
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(label)
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
    let review: CustomerOrderReview?
    let onLeaveReview: () -> Void

    var body: some View {
        OrderCardSurface {
            OrderSectionHeaderRow(title: L10n.OrderDetail.yourReview)
            if let review {
                StarsRow(rating: review.rating)
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
    let order: CustomerOrderDetail
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
