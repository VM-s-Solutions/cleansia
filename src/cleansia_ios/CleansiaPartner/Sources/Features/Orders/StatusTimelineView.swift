import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct StatusTimelineEntry: Equatable {
    let label: String
    let timestamp: Date?
    let isCurrent: Bool
}

enum StatusTimelineFormat {
    /// Sort history by `createdOn` ascending, drop entries with no timestamp,
    /// and mark the last (most recent) as current (the `StatusTimeline.kt`
    /// parity). Returns [] when there's nothing to show.
    static func entries(from history: [OrderStatusTrackDto]) -> [StatusTimelineEntry] {
        let dated = history.compactMap { track -> (OrderStatusTrackDto, Date)? in
            guard let createdOn = track.createdOn else { return nil }
            return (track, createdOn)
        }
        .sorted { $0.1 < $1.1 }

        let lastIndex = dated.count - 1
        return dated.enumerated().map { index, pair in
            let (track, createdOn) = pair
            return StatusTimelineEntry(
                label: OrderStatusLabel.label(name: track.status?.name, value: track.status?.value),
                timestamp: createdOn,
                isCurrent: index == lastIndex
            )
        }
    }
}

struct StatusTimelineView: View {
    let history: [OrderStatusTrackDto]

    private var entries: [StatusTimelineEntry] {
        StatusTimelineFormat.entries(from: history)
    }

    var body: some View {
        if !entries.isEmpty {
            OrderSectionCard(title: L10n.Orders.statusTimelineSectionTitle, systemImage: "clock.arrow.circlepath") {
                VStack(alignment: .leading, spacing: 0) {
                    ForEach(Array(entries.enumerated()), id: \.offset) { index, entry in
                        TimelineRow(entry: entry, isLast: index == entries.count - 1)
                    }
                }
            }
        }
    }
}

private struct TimelineRow: View {
    let entry: StatusTimelineEntry
    let isLast: Bool

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            VStack(spacing: 0) {
                dot
                if !isLast {
                    Rectangle()
                        .fill(CleansiaColors.outlineVariant)
                        .frame(width: 2, height: 28)
                }
            }
            VStack(alignment: .leading, spacing: 2) {
                Text(entry.label)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(entry.isCurrent ? CleansiaColors.onSurface : CleansiaColors.onSurfaceVariant)
                if let timestamp = entry.timestamp {
                    Text(OrdersFormat.relativeDateTime(timestamp))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            .padding(.bottom, isLast ? 0 : Spacing.s)
            Spacer()
        }
    }

    @ViewBuilder
    private var dot: some View {
        if entry.isCurrent {
            Circle()
                .fill(CleansiaColors.primary)
                .frame(width: 20, height: 20)
        } else {
            Circle()
                .fill(CleansiaColors.successText)
                .frame(width: 20, height: 20)
                .overlay(
                    Image(systemName: "checkmark")
                        .font(.system(size: 10, weight: .bold))
                        .foregroundColor(.white)
                )
        }
    }
}
