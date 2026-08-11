import SwiftUI

// The clean's Live Activity card. It lives in Core rather than in the widget extension for two reasons:
// the extension is a separate binary that links Core (so this is the one copy both the app and the widget
// can read, for art and copy alike), and a widget target has no tests — here the views can be rendered.
//
// Type sizes are deliberately `system`, never `CleansiaTypography`: the app registers Poppins/Nunito from
// its own bundle, the extension has neither, and `CleansiaFont` falls back silently — so app typography in
// a widget renders as system fonts that only LOOK intentional.

/// Everything the card draws, resolved from one content-state.
public struct LiveActivityCardModel: Equatable, Sendable {
    public let card: LiveActivityCard
    public let orderNumber: String
    public let finish: Date
    /// The window the current leg fills over and the island counts down. Nil once nothing is left to time.
    public let liveRange: ClosedRange<Date>?

    public init(card: LiveActivityCard, orderNumber: String, finish: Date, liveRange: ClosedRange<Date>?) {
        self.card = card
        self.orderNumber = orderNumber
        self.finish = finish
        self.liveRange = liveRange
    }

    public var orderLabel: String {
        orderNumber.isEmpty ? LiveActivityL10n.bookingFallback : LiveActivityL10n.orderNumber(orderNumber)
    }
}

/// The lock-screen presentation, and the content the expanded Dynamic Island rebuilds region by region.
public struct LiveActivityCleanCard: View {
    private let model: LiveActivityCardModel

    public init(model: LiveActivityCardModel) {
        self.model = model
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(alignment: .top, spacing: 6) {
                LiveActivityBrandLockup()

                Spacer(minLength: 6)

                if model.card.showsFinishTime {
                    LiveActivityFinishTime(finish: model.finish, compact: false)
                }
            }

            VStack(alignment: .leading, spacing: 2) {
                LiveActivityHeadlineLine(headline: model.card.headline)
                HStack(alignment: .firstTextBaseline, spacing: 8) {
                    if let detail = model.card.detail {
                        Text(detail)
                            .font(.caption)
                            .foregroundColor(.secondary)
                            .lineLimit(1)
                            .minimumScaleFactor(0.8)
                    }
                    Spacer(minLength: 4)
                    LiveActivityOrderLabel(model: model)
                }
            }

            if let position = model.card.position {
                LiveActivityStepperBar(position: position, liveRange: model.liveRange)
            }
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 14)
    }
}

public struct LiveActivityOrderLabel: View {
    private let model: LiveActivityCardModel

    public init(model: LiveActivityCardModel) {
        self.model = model
    }

    public var body: some View {
        Text(model.orderLabel)
            .font(.caption2)
            .foregroundColor(.secondary)
            .lineLimit(1)
            .minimumScaleFactor(0.8)
    }
}

/// The compact Dynamic Island's readout: direction A's countdown, in the slot most glances land on. It
/// falls back to the wall-clock finish rather than a bare count-UP — an unlabelled number climbing away
/// from zero in a slot this narrow reads as time remaining, and lies.
public struct LiveActivityCompactReadout: View {
    private let model: LiveActivityCardModel

    public init(model: LiveActivityCardModel) {
        self.model = model
    }

    public var body: some View {
        if let range = model.liveRange {
            Text(timerInterval: range, countsDown: true)
                .font(.caption2.weight(.semibold).monospacedDigit())
                .foregroundColor(CleansiaColors.primary)
                .frame(maxWidth: 44)
                .multilineTextAlignment(.trailing)
        } else if model.card.showsFinishTime {
            LiveActivityFinishTime(finish: model.finish, compact: true)
        } else {
            Image(systemName: symbol).foregroundColor(CleansiaColors.primary)
        }
    }

    private var symbol: String {
        switch model.card {
        case .journey(.done): "checkmark.seal.fill"
        case .cancelled: "xmark.circle.fill"
        default: "sparkles"
        }
    }
}

public struct LiveActivityBrandLockup: View {
    public init() {}

    public var body: some View {
        HStack(spacing: 6) {
            ZStack {
                Circle().fill(CleansiaColors.primaryContainer)
                Mascot.waving.image.resizable().scaledToFit().padding(1)
            }
            .frame(width: 22, height: 22)

            Text(verbatim: "Cleansia")
                .font(.caption.weight(.semibold))
                .foregroundColor(CleansiaColors.primary)
        }
    }
}

/// The wall-clock instant the clean is expected to end. Static by construction: a time that does not tick
/// cannot be caught looking frozen while a queued update is still in flight.
public struct LiveActivityFinishTime: View {
    private let finish: Date
    private let compact: Bool

    public init(finish: Date, compact: Bool) {
        self.finish = finish
        self.compact = compact
    }

    public var body: some View {
        VStack(alignment: .trailing, spacing: 0) {
            if !compact {
                Text(LiveActivityL10n.finish)
                    .font(.caption2)
                    .foregroundColor(.secondary)
            }
            // Tabular figures rather than a monospaced FACE: the clock must not jitter as the digits
            // change, but a monospaced colon sets the two halves an em apart and reads as "17 : 35".
            Text(finish, style: .time)
                .font(.system(compact ? .caption : .title3, design: .rounded).weight(.bold).monospacedDigit())
                .foregroundColor(CleansiaColors.primary)
        }
    }
}

/// The line the card leads with. An in-service clean reads as the move it just made — where it came from,
/// dimmed, then where it is — so the card narrates rather than announces.
public struct LiveActivityHeadlineLine: View {
    private let headline: LiveActivityHeadline

    public init(headline: LiveActivityHeadline) {
        self.headline = headline
    }

    public var body: some View {
        switch headline {
        case let .transition(from, to):
            HStack(spacing: 5) {
                Text(from.label).foregroundColor(.secondary)
                Image(systemName: "arrow.right").font(.caption2).foregroundColor(.secondary)
                Text(to.label).foregroundColor(.primary)
            }
            .font(.headline)
            .lineLimit(1)
            .minimumScaleFactor(0.7)
        case .completed:
            title(LiveActivityL10n.Status.completedTitle)
        case .cancelled:
            title(LiveActivityL10n.Status.cancelledTitle)
        case .generic:
            title(LiveActivityL10n.Status.genericTitle)
        }
    }

    private func title(_ text: String) -> some View {
        Text(text)
            .font(.headline)
            .foregroundColor(.primary)
            .lineLimit(1)
            .minimumScaleFactor(0.7)
    }
}

/// The four legs of the clean: a segment per leg over a row of labels.
///
/// The segment the order is ON is a `ProgressView(timerInterval:)` — system-drawn, so it keeps advancing on
/// a locked device with no push and no app running. That is what makes a late update survivable: the bar is
/// still moving while the queue takes its time, and a leg that lights up a few seconds late is merely late,
/// where a re-anchored number would visibly jump.
public struct LiveActivityStepperBar: View {
    private let position: LiveActivityStep
    private let liveRange: ClosedRange<Date>?

    public init(position: LiveActivityStep, liveRange: ClosedRange<Date>?) {
        self.position = position
        self.liveRange = liveRange
    }

    public var body: some View {
        VStack(spacing: 5) {
            HStack(spacing: 4) {
                ForEach(LiveActivityStep.allCases, id: \.rawValue, content: segment)
            }
            .frame(height: 4)

            HStack(spacing: 4) {
                ForEach(LiveActivityStep.allCases, id: \.rawValue, content: legLabel)
            }
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(LiveActivityL10n.stepOf(position.number, LiveActivityStep.count))
    }

    /// Only the leg being walked is a `ProgressView`; the rest are plain capsules. The system draws the
    /// timer-interval bar (which is the whole point — it advances with no push), but nothing else here
    /// needs that, and a shape is drawable everywhere a bar is not.
    @ViewBuilder
    private func segment(_ step: LiveActivityStep) -> some View {
        if step.leg(at: position) == .current, let liveRange {
            ProgressView(timerInterval: liveRange, countsDown: false) {} currentValueLabel: {}
                .progressViewStyle(.linear)
                .tint(CleansiaColors.primary)
        } else {
            Capsule()
                .fill(step.leg(at: position) == .upcoming
                    ? CleansiaColors.primary.opacity(0.22)
                    : CleansiaColors.primary)
                .frame(height: 4)
        }
    }

    private func legLabel(_ step: LiveActivityStep) -> some View {
        let leg = step.leg(at: position)
        return Text(step.label)
            .font(.system(size: 9, weight: leg == .current ? .semibold : .regular))
            .foregroundColor(leg == .upcoming ? .secondary : CleansiaColors.primary)
            .lineLimit(1)
            .minimumScaleFactor(0.6)
            .frame(maxWidth: .infinity)
    }
}

/// The compact Dynamic Island's position readout: one dot per leg, filled up to where the order is.
///
/// The written "Step 3 of 4" cannot share the compact slot with a clock — "Krok 3 z 4" plus digits overruns
/// it — so the dots carry the position there, and the words carry it in the expanded island and to
/// VoiceOver.
public struct LiveActivityStepDots: View {
    private let position: LiveActivityStep?

    public init(position: LiveActivityStep?) {
        self.position = position
    }

    public var body: some View {
        HStack(spacing: 2.5) {
            ForEach(LiveActivityStep.allCases, id: \.rawValue) { step in
                Circle()
                    .fill(fill(step))
                    .frame(width: 5, height: 5)
            }
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(spokenPosition)
    }

    private func fill(_ step: LiveActivityStep) -> Color {
        guard let position else { return CleansiaColors.primary.opacity(0.25) }
        return switch step.leg(at: position) {
        case .complete, .current: CleansiaColors.primary
        case .upcoming: CleansiaColors.primary.opacity(0.25)
        }
    }

    private var spokenPosition: String {
        guard let position else { return LiveActivityL10n.Status.genericTitle }
        return LiveActivityL10n.stepOf(position.number, LiveActivityStep.count)
    }
}
