import SwiftUI

/// Which of the three semantic groups an order status belongs to.
///
/// The two apps generate their own `OrderStatus` from their own API contract, so — exactly as
/// `OrderTrackerRule` does — the mapping stays in each app and only the GROUP crosses into
/// `CleansiaCore`.
public enum OrderStatusTone: Equatable {
    /// New · Pending · Confirmed · On the way · In progress — the order is still moving.
    case live
    /// Completed — terminal, and it went well.
    case done
    /// Cancelled — terminal, and it did not.
    case failed
    /// A status the app could not resolve. Drawn with no mark: there is nothing to assert.
    case unknown
}

enum StatusMarkMetrics {
    static let glyph: CGFloat = 10
    static let liveDot: CGFloat = 7
    static let fillOpacity: Double = 0.14
    static let edgeOpacity: Double = 0.30
    static let point: CGFloat = 20
    static let pointCore: CGFloat = 10
    static let haloWidth: CGFloat = 2
    static let hollowWidth: CGFloat = 1.5
}

/// The one order-status badge, for every status pill in both apps.
///
/// **The MARK says which group the status is in; the hue only agrees with it.** A filled dot is a live
/// order, a check is a completed one, a cross is a cancelled one — three different SHAPES, so the
/// grouping survives a colour-blind reader and survives the app using the same blue for the brand and
/// for an in-flight order. That is the single device: everything else (capsule, size, type ramp, ink)
/// is identical for every status, which is also what makes one badge out of what used to be three
/// look-alike implementations.
///
/// The ink is `onSurface` rather than the tint on purpose. Same-hue text on a 14% wash of itself
/// measures about 2:1 for the amber and sky statuses — under every contrast floor — and it was the
/// reason the old pills read as "pale" at every size.
public struct OrderStatusBadge: View {
    private let label: String
    private let tint: Color
    private let tone: OrderStatusTone

    public init(label: String, tint: Color, tone: OrderStatusTone) {
        self.label = label
        self.tint = tint
        self.tone = tone
    }

    public var body: some View {
        HStack(spacing: Spacing.xxs) {
            mark
            Text(label)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurface)
        }
        .padding(.horizontal, Spacing.xs)
        .padding(.vertical, Spacing.xxs)
        .background(tint.opacity(StatusMarkMetrics.fillOpacity), in: Capsule())
        .overlay(Capsule().stroke(tint.opacity(StatusMarkMetrics.edgeOpacity), lineWidth: 1))
        // The mark is redundant with the label, so the badge announces as ONE element carrying the
        // status text — the same thing VoiceOver read when the pill was a bare `Text`.
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(Text(label))
    }

    @ViewBuilder
    private var mark: some View {
        switch tone {
        case .live:
            Circle()
                .fill(tint)
                .frame(width: StatusMarkMetrics.liveDot, height: StatusMarkMetrics.liveDot)
        case .done:
            glyph("checkmark")
        case .failed:
            glyph("xmark")
        case .unknown:
            EmptyView()
        }
    }

    private func glyph(_ name: String) -> some View {
        Image(systemName: name)
            .font(.system(size: StatusMarkMetrics.glyph, weight: .bold))
            .foregroundColor(tint)
    }
}

/// One point on an order's status timeline, shared by the customer card and the partner sheet.
///
/// Past / current / future are told apart by STRUCTURE, not fill colour: a past step is a washed disc
/// carrying a check, the current step is a bullseye — solid core inside a ringed halo — and a future
/// step is an empty outline. The old points differed only in fill, which is why the current step never
/// read as current at a glance.
///
/// Deliberately quiet: no pulse, no shadow, no animation. This sits on screen for the length of a job.
///
/// It reuses `OrderTrackerSegment` rather than declaring a second past/current/future enum — same three
/// states, same two apps.
public struct OrderTimelinePoint: View {
    private let segment: OrderTrackerSegment
    private let tint: Color

    public init(segment: OrderTrackerSegment, tint: Color = CleansiaColors.primary) {
        self.segment = segment
        self.tint = tint
    }

    public var body: some View {
        shape
            .frame(width: StatusMarkMetrics.point, height: StatusMarkMetrics.point)
            // Decorative: the row beside it already carries the status name and its timestamp, and
            // labelling the point would need a word ("completed step") that does not exist in the
            // catalogue. Hidden rather than announced as an unnamed image.
            .accessibilityHidden(true)
    }

    @ViewBuilder
    private var shape: some View {
        switch segment {
        case .past:
            Circle()
                .fill(tint.opacity(StatusMarkMetrics.fillOpacity))
                .overlay(Circle().stroke(tint.opacity(StatusMarkMetrics.edgeOpacity), lineWidth: 1))
                .overlay(
                    Image(systemName: "checkmark")
                        .font(.system(size: StatusMarkMetrics.glyph, weight: .bold))
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                )
        case .current:
            Circle()
                .fill(tint.opacity(StatusMarkMetrics.fillOpacity))
                .overlay(
                    Circle().stroke(
                        tint.opacity(StatusMarkMetrics.edgeOpacity),
                        lineWidth: StatusMarkMetrics.haloWidth
                    )
                )
                .overlay(
                    Circle()
                        .fill(tint)
                        .frame(width: StatusMarkMetrics.pointCore, height: StatusMarkMetrics.pointCore)
                )
        case .future:
            Circle()
                .stroke(CleansiaColors.outlineVariant, lineWidth: StatusMarkMetrics.hollowWidth)
        }
    }
}

#if DEBUG
    struct OrderStatusMarks_Previews: PreviewProvider {
        static var previews: some View {
            VStack(alignment: .leading, spacing: Spacing.m) {
                OrderStatusBadge(label: "In progress", tint: CleansiaColors.secondary, tone: .live)
                OrderStatusBadge(label: "Completed", tint: CleansiaColors.successText, tone: .done)
                OrderStatusBadge(label: "Cancelled", tint: CleansiaColors.error, tone: .failed)
                HStack(spacing: Spacing.s) {
                    OrderTimelinePoint(segment: .past, tint: CleansiaColors.successText)
                    OrderTimelinePoint(segment: .current)
                    OrderTimelinePoint(segment: .future)
                }
            }
            .padding()
            .background(CleansiaColors.surface)
        }
    }
#endif
