import SwiftUI

/// One phase of the five-phase tracker.
public enum OrderTrackerSegment: Equatable {
    case past, current, future
}

/// What the bar draws: five segments with a step number, or the single flat bar a cancelled order gets.
///
/// One sealed answer rather than a segment list plus a cancelled flag, so a renderer cannot forget the
/// cancelled branch.
public enum OrderTrackerState: Equatable {
    case cancelled
    case steps(segments: [OrderTrackerSegment], stepNumber: Int)
}

/// The phase→segments rule, shared by both apps.
///
/// It takes a step INDEX rather than an order status because the two apps do not share a status type:
/// each generates its own from its own API contract. Each maps its statuses to an index and hands over
/// the already-translated labels.
public enum OrderTrackerRule {
    public static let stepCount = 5

    public static func state(step: Int, cancelled: Bool, completed: Bool) -> OrderTrackerState {
        guard !cancelled else { return .cancelled }
        let index = max(0, min(step, stepCount - 1))
        let segments = (0 ..< stepCount).map { position -> OrderTrackerSegment in
            if completed || position < index { return .past }
            return position == index ? .current : .future
        }
        return .steps(segments: segments, stepNumber: index + 1)
    }
}

enum TrackerMetrics {
    static let barHeight: CGFloat = 10
    static let corner: CGFloat = 5
    static let gap: CGFloat = 6
    static let counterGap: CGFloat = 4
}

public struct OrderTrackerBar: View {
    private let state: OrderTrackerState
    /// Nil draws the bars alone — for callers that put the counter on their own headline's line, so the
    /// bar sits flush under the phase it describes instead of below a band with an empty left half.
    private let stepCounterLabel: ((Int, Int) -> String)?
    private let cancelledLabel: String
    private let currentFraction: Double?
    private let isOverEstimate: Bool

    public init(
        state: OrderTrackerState,
        cancelledLabel: String,
        stepCounterLabel: ((Int, Int) -> String)? = nil,
        currentFraction: Double? = nil,
        isOverEstimate: Bool = false
    ) {
        self.state = state
        self.stepCounterLabel = stepCounterLabel
        self.cancelledLabel = cancelledLabel
        self.currentFraction = currentFraction
        self.isOverEstimate = isOverEstimate
    }

    public var body: some View {
        switch state {
        case .cancelled:
            CancelledTrack(label: cancelledLabel)
        case let .steps(segments, stepNumber):
            VStack(alignment: .trailing, spacing: TrackerMetrics.counterGap) {
                if let stepCounterLabel {
                    Text(stepCounterLabel(stepNumber, OrderTrackerRule.stepCount))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                HStack(spacing: TrackerMetrics.gap) {
                    ForEach(Array(segments.enumerated()), id: \.offset) { _, segment in
                        TrackerSegment(state: segment, fraction: currentFraction, isOverEstimate: isOverEstimate)
                    }
                }
            }
            .frame(maxWidth: .infinity, alignment: .trailing)
        }
    }
}

private struct TrackerSegment: View {
    let state: OrderTrackerSegment
    let fraction: Double?
    let isOverEstimate: Bool

    var body: some View {
        switch state {
        case .past:
            flatBar(CleansiaColors.primary, border: CleansiaColors.primaryText)
        case .future:
            flatBar(CleansiaColors.surfaceVariant, border: CleansiaColors.outline)
        case .current:
            GeometryReader { geometry in
                ZStack(alignment: .leading) {
                    RoundedRectangle(cornerRadius: TrackerMetrics.corner)
                        .fill(tint.opacity(0.1))
                    if let fraction {
                        RoundedRectangle(cornerRadius: TrackerMetrics.corner)
                            .fill(tint)
                            .frame(width: geometry.size.width * min(1, max(0, fraction)))
                    }
                }
                .overlay(RoundedRectangle(cornerRadius: TrackerMetrics.corner).strokeBorder(
                    isOverEstimate ? CleansiaColors.pendingInk : CleansiaColors.primaryText,
                    lineWidth: 2
                ))
            }
            .frame(height: TrackerMetrics.barHeight)
        }
    }

    private func flatBar(_ color: Color, border: Color) -> some View {
        RoundedRectangle(cornerRadius: TrackerMetrics.corner)
            .fill(color)
            .overlay(RoundedRectangle(cornerRadius: TrackerMetrics.corner).strokeBorder(border, lineWidth: 2))
            .frame(height: TrackerMetrics.barHeight)
    }

    private var tint: Color {
        isOverEstimate ? CleansiaColors.warningStar : CleansiaColors.primary
    }
}

private struct CancelledTrack: View {
    let label: String

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(label)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.error)
            RoundedRectangle(cornerRadius: TrackerMetrics.corner)
                .fill(CleansiaColors.error)
                .frame(height: TrackerMetrics.barHeight)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}
