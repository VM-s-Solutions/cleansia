import SwiftUI

/// One phase of the tracker.
public enum OrderTrackerSegment: Equatable {
    case past, current, future
}

/// What the bar draws: its segments with a step number, or the single flat bar a cancelled order gets.
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
/// the already-translated labels. The customer's order has five steps; a caller with fewer passes its count.
public enum OrderTrackerRule {
    public static let stepCount = 5

    public static func state(
        step: Int,
        cancelled: Bool,
        completed: Bool,
        stepCount: Int = OrderTrackerRule.stepCount
    ) -> OrderTrackerState {
        guard !cancelled else { return .cancelled }
        let stepCount = max(stepCount, 1)
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
    static let travelStart: CGFloat = -0.5
    static let travelEnd: CGFloat = 2.5
    static let cycleSeconds: Double = 1.3
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
                    Text(stepCounterLabel(stepNumber, segments.count))
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
            flatBar(CleansiaColors.primary)
        case .future:
            flatBar(CleansiaColors.outlineVariant)
        case .current:
            SweepingTrackerSegment(fraction: fraction, tint: tint)
        }
    }

    private func flatBar(_ color: Color) -> some View {
        RoundedRectangle(cornerRadius: TrackerMetrics.corner)
            .fill(color)
            .frame(height: TrackerMetrics.barHeight)
    }

    private var tint: Color {
        isOverEstimate ? CleansiaColors.warningStar : CleansiaColors.primary
    }
}

private struct SweepingTrackerSegment: View {
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @Environment(\.scenePhase) private var scenePhase
    let fraction: Double?
    let tint: Color

    private var animates: Bool {
        !reduceMotion && scenePhase == .active
    }

    var body: some View {
        TimelineView(.animation(minimumInterval: 1.0 / 30, paused: !animates)) { context in
            GeometryReader { geometry in
                let time = context.date.timeIntervalSinceReferenceDate
                let cycle = time.truncatingRemainder(dividingBy: TrackerMetrics.cycleSeconds) /
                    TrackerMetrics.cycleSeconds
                let travel = TrackerMetrics.travelStart +
                    (TrackerMetrics.travelEnd - TrackerMetrics.travelStart) * CGFloat(cycle)
                ZStack(alignment: .leading) {
                    Rectangle().fill(tint.opacity(0.22))
                    if let fraction {
                        Rectangle()
                            .fill(tint)
                            .frame(width: geometry.size.width * min(1, max(0, fraction)))
                            .opacity(animates ? 0.94 + 0.06 * cos(time * .pi) : 1)
                    }
                    if animates {
                        Rectangle()
                            .fill(LinearGradient(
                                colors: [tint.opacity(0), tint.opacity(0.18), tint.opacity(0.30)],
                                startPoint: .leading,
                                endPoint: .trailing
                            ))
                            .frame(width: geometry.size.width * TrackerMetrics.bandWidthFraction)
                            .offset(x: geometry.size.width * travel)
                    }
                }
            }
            .frame(height: TrackerMetrics.barHeight)
            .clipShape(RoundedRectangle(cornerRadius: TrackerMetrics.corner))
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
