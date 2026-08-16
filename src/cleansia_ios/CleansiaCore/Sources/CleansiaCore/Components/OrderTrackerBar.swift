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
    static let barHeight: CGFloat = 4
    static let corner: CGFloat = 2
    static let gap: CGFloat = 6
    static let counterGap: CGFloat = 4
    static let bandWidthFraction: CGFloat = 0.5
    /// The band enters from off-screen left and finishes well past the right edge, so half of each
    /// cycle is a rest beat on the armed track.
    static let travelStart: CGFloat = -0.5
    static let travelEnd: CGFloat = 2.5
    static let cycleSeconds: Double = 1.3
}

/// Segmented progress bar, one thin bar per phase: past solid, current sweeping, future muted.
///
/// **No shimmer, no looping, no halo** — the bar is intentionally quiet so the reader's eye stays on the
/// job, not the chrome. Completed fills every segment rather than sitting on the last; cancelled drops
/// the segmentation entirely, because a dead order has no progress to show.
///
/// Twin: `OrderTrackerBar` (Android, `:core`). Keep the two in step.
public struct OrderTrackerBar: View {
    private let state: OrderTrackerState
    private let stepCounterLabel: (Int, Int) -> String
    private let cancelledLabel: String

    public init(
        state: OrderTrackerState,
        cancelledLabel: String,
        stepCounterLabel: @escaping (Int, Int) -> String
    ) {
        self.state = state
        self.stepCounterLabel = stepCounterLabel
        self.cancelledLabel = cancelledLabel
    }

    public var body: some View {
        switch state {
        case .cancelled:
            CancelledTrack(label: cancelledLabel)
        case let .steps(segments, stepNumber):
            VStack(alignment: .trailing, spacing: TrackerMetrics.counterGap) {
                Text(stepCounterLabel(stepNumber, OrderTrackerRule.stepCount))
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                HStack(spacing: TrackerMetrics.gap) {
                    ForEach(Array(segments.enumerated()), id: \.offset) { _, segment in
                        TrackerSegment(state: segment)
                    }
                }
            }
            .frame(maxWidth: .infinity, alignment: .trailing)
        }
    }
}

private struct TrackerSegment: View {
    let state: OrderTrackerSegment

    var body: some View {
        switch state {
        case .past:
            flatBar(CleansiaColors.primary)
        case .future:
            flatBar(CleansiaColors.outlineVariant)
        case .current:
            SweepingTrackerSegment()
        }
    }

    private func flatBar(_ color: Color) -> some View {
        RoundedRectangle(cornerRadius: TrackerMetrics.corner)
            .fill(color)
            .frame(height: TrackerMetrics.barHeight)
    }
}

/// The current phase: an armed track under a soft band gliding left to right on a linear loop. Quiet by
/// design — it sits on screen for a multi-hour job.
private struct SweepingTrackerSegment: View {
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var sweptToEnd = false

    var body: some View {
        GeometryReader { geometry in
            let width = geometry.size.width
            Rectangle()
                .fill(
                    LinearGradient(
                        colors: [
                            CleansiaColors.primary.opacity(0),
                            CleansiaColors.primary.opacity(0.18),
                            CleansiaColors.primary.opacity(0.30)
                        ],
                        startPoint: .leading,
                        endPoint: .trailing
                    )
                )
                .frame(width: width * TrackerMetrics.bandWidthFraction)
                .offset(x: width * (sweptToEnd ? TrackerMetrics.travelEnd : TrackerMetrics.travelStart))
        }
        .frame(height: TrackerMetrics.barHeight)
        .background(CleansiaColors.primary.opacity(0.22))
        .clipShape(RoundedRectangle(cornerRadius: TrackerMetrics.corner))
        .onAppear {
            guard !reduceMotion else { return }
            withAnimation(
                .linear(duration: TrackerMetrics.cycleSeconds).repeatForever(autoreverses: false)
            ) {
                sweptToEnd = true
            }
        }
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
