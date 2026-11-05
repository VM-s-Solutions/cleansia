import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UIKit

extension OrdersBoardRules {
    /// A job's minutes on its own day: 14:00–16:00 is 840–960.
    static func minuteSpan(
        start: Date,
        end: Date,
        calendar: Calendar = .current
    ) -> (start: Int, end: Int) {
        let clock = calendar.dateComponents([.hour, .minute], from: start)
        let from = (clock.hour ?? 0) * 60 + (clock.minute ?? 0)
        return (from, from + max(Int(end.timeIntervalSince(start) / 60), 0))
    }

    /// 07:00–19:00, widened to the whole hour that holds the earliest block and the latest end. A page passes
    /// every block it draws, so its days compare on one scale.
    static func dayRange(_ spans: [(start: Int, end: Int)]) -> (from: Int, to: Int) {
        spans.reduce((from: 7 * 60, to: 19 * 60)) { range, span in
            let from = max(Int((Double(span.start) / 60).rounded(.down)) * 60, 0)
            let to = min(Int((Double(span.end) / 60).rounded(.up)) * 60, 24 * 60)
            return (min(range.from, from), max(range.to, to))
        }
    }

    /// The even hours inside the range; every third once it passes fourteen hours, so they never crowd.
    static func dayLineHours(_ range: (from: Int, to: Int)) -> [Int] {
        let step = range.to - range.from > 14 * 60 ? 3 : 2
        let first = Int((Double(range.from / 60 + 1) / Double(step)).rounded(.up)) * step
        return Array(stride(from: first, to: Int((Double(range.to) / 60).rounded(.up)), by: step))
            .filter { $0 * 60 < range.to }
    }
}

/// The working day as a strip under each day: her booked time solid sky, open jobs as dashed slots, a clash as one
/// red block, the hour she may set off as a pale band, and now as an ink tick. Marks are never under 12 pt, and two
/// whose drawn boxes touch take the upper and lower half of the track. Hours under it; the now and set-off times over
/// it when asked, both in the caption token. Decorative to VoiceOver: the words beside it say everything.
struct OrdersDayLine: View {
    @Environment(\.dynamicTypeSize) private var typeSize
    let range: (from: Int, to: Int)
    var mine: [(start: Int, end: Int)] = []
    var open: [(start: Int, end: Int)] = []
    var clashes: [(start: Int, end: Int)] = []
    var setOff: (start: Int, end: Int)?
    var now: Int?
    var labels: [Label] = []

    /// A time over the line: now in ink, the set-off hour in blue.
    struct Label {
        let minute: Int
        let text: String
        var isSetOff = false
    }

    /// The caption token's line at the capped text size: the times over the track and the hours under it.
    private var captionLine: CGFloat {
        let style = CleansiaTypography.scale.labelMedium
        let traits = UITraitCollection(
            preferredContentSizeCategory: UIContentSizeCategory(min(typeSize, .xxxLarge))
        )
        let size = UIFontMetrics.default.scaledValue(for: style.size, compatibleWith: traits).rounded()
        return CleansiaFont.uiFont(style.family, weight: style.weight, size: size).lineHeight.rounded(.up)
    }

    private var top: CGFloat {
        labels.isEmpty ? 4 : captionLine + 3
    }

    var body: some View {
        Canvas { context, size in
            draw(in: context, size: size)
        }
        .frame(height: top + 10 + 2 + captionLine)
        .dynamicTypeSize(...DynamicTypeSize.xxxLarge)
        .accessibilityHidden(true)
    }

    private func x(_ minute: Int, _ width: CGFloat) -> CGFloat {
        Self.x(minute, range: range, track: width)
    }

    static func x(_ minute: Int, range: (from: Int, to: Int), track: CGFloat) -> CGFloat {
        let span = CGFloat(max(range.to - range.from, 1))
        return CGFloat(min(max(minute, range.from), range.to) - range.from) / span * track
    }

    /// A span's mark, 1 pt in from each end; never under 12 pt, so a 7-minute job still shows: a shorter one is
    /// centred on its span and kept on the track.
    static func mark(from: CGFloat, to: CGFloat, track: CGFloat) -> (x: CGFloat, width: CGFloat) {
        let least: CGFloat = 12
        guard to - from - 2 < least else { return (from + 1, to - from - 2) }
        return (min(max((from + to - least) / 2, 0), max(track - least, 0)), least)
    }

    private func box(_ span: (start: Int, end: Int), _ width: CGFloat, top: CGFloat, height: CGFloat) -> CGRect {
        let mark = Self.mark(from: x(span.start, width), to: x(span.end, width), track: width)
        return CGRect(x: mark.x, y: top, width: mark.width, height: height)
    }

    private func laned(_ rect: CGRect, _ lane: Lane) -> CGRect {
        switch lane {
        case .full: rect
        case .upper: CGRect(x: rect.minX, y: rect.minY, width: rect.width, height: rect.height / 2 - 0.5)
        case .lower: CGRect(x: rect.minX, y: rect.midY + 0.5, width: rect.width, height: rect.height / 2 - 0.5)
        }
    }

    private func draw(in context: GraphicsContext, size: CGSize) {
        let track = CGRect(x: 0, y: top, width: size.width, height: 10)
        let trackPath = Path(roundedRect: track, cornerRadius: 5)
        context.fill(trackPath, with: .color(CleansiaColors.surfaceVariant))
        if let now, now > range.from {
            var past = context
            past.clip(to: trackPath)
            past.fill(
                Path(CGRect(x: 0, y: top, width: x(now, size.width), height: 10)),
                with: .color(CleansiaColors.outline)
            )
        }
        if let setOff {
            let band = box(setOff, size.width, top: top, height: 10)
            context.fill(Path(roundedRect: band, cornerRadius: 5), with: .color(CleansiaColors.dayLineSetOff))
        }
        let marks = open.map { (span: $0, mine: false) } + mine.map { (span: $0, mine: true) }
        let lanes = Self.lanes(marks.map { Self.drawn($0.span, mine: $0.mine, range: range, track: size.width) })
        for (mark, lane) in zip(marks, lanes) {
            let rect = laned(box(mark.span, size.width, top: top, height: 10), lane)
            let radius = min(rect.height, rect.width) / 2
            if mark.mine {
                context.fill(Path(roundedRect: rect, cornerRadius: radius), with: .color(CleansiaColors.primary))
            } else {
                let path = Path(roundedRect: rect.insetBy(dx: 0.75, dy: 0.75), cornerRadius: max(radius - 0.75, 0))
                context.fill(path, with: .color(CleansiaColors.surface))
                let dashed = StrokeStyle(lineWidth: 1.5, dash: [3, 2])
                context.stroke(path, with: .color(CleansiaColors.quiet), style: dashed)
            }
        }
        for span in clashes {
            let block = box(span, size.width, top: top - 2, height: 14)
            context.fill(Path(roundedRect: block, cornerRadius: 7), with: .color(CleansiaColors.error))
        }
        if let now, now > range.from, now < range.to {
            let tick = CGRect(x: x(now, size.width) - 1, y: top - 4, width: 2, height: 18)
            context.fill(Path(roundedRect: tick, cornerRadius: 1), with: .color(CleansiaColors.onSurface))
        }
        for hour in OrdersBoardRules.dayLineHours(range) {
            let text = Text(verbatim: "\(hour)")
                .font(CleansiaTypography.labelMedium)
                .monospacedDigit()
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            context.draw(text, at: CGPoint(x: x(hour * 60, size.width), y: top + 12), anchor: .top)
        }
        drawLabels(in: context, width: size.width)
    }

    private func drawLabels(in context: GraphicsContext, width: CGFloat) {
        for label in labels {
            let pointX = x(label.minute, width)
            let share = width > 0 ? pointX / width : 0
            let anchor: UnitPoint = share < 0.08 ? .topLeading : share > 0.92 ? .topTrailing : .top
            let text = Text(verbatim: label.text)
                .font(CleansiaTypography.labelMedium)
                .monospacedDigit()
                .foregroundColor(label.isSetOff ? CleansiaColors.primaryText : CleansiaColors.onSurface)
            context.draw(text, at: CGPoint(x: pointX, y: 0), anchor: anchor)
        }
    }
}

extension OrdersDayLine {
    /// Where a mark is drawn on a track, with its span and whose it is.
    struct DrawnMark {
        let left: CGFloat
        let width: CGFloat
        let span: (start: Int, end: Int)
        let mine: Bool
    }

    enum Lane: Equatable {
        case full
        case upper
        case lower
    }

    static func drawn(
        _ span: (start: Int, end: Int),
        mine: Bool,
        range: (from: Int, to: Int),
        track: CGFloat
    ) -> DrawnMark {
        let box = mark(
            from: x(span.start, range: range, track: track),
            to: x(span.end, range: range, track: track),
            track: track
        )
        return DrawnMark(left: box.x, width: box.width, span: span, mine: mine)
    }

    /// Two marks whose drawn boxes touch take the upper and lower half of the track: hers above an open one, else the
    /// earlier above. Her job against an open slot it overlaps in time is the clash, drawn as one red block instead.
    static func lanes(_ marks: [DrawnMark]) -> [Lane] {
        var lanes = Array(repeating: Lane.full, count: marks.count)
        for first in marks.indices {
            for second in marks.indices where second > first {
                let one = marks[first], two = marks[second]
                let touch = one.left < two.left + two.width + 1 && two.left < one.left + one.width + 1
                let timed = one.span.start < two.span.end && two.span.start < one.span.end
                guard touch, !(timed && one.mine != two.mine) else { continue }
                let firstUp = one.mine != two.mine ? one.mine : one.left <= two.left
                let (above, below) = firstUp ? (first, second) : (second, first)
                switch (lanes[above], lanes[below]) {
                case (.full, .full): (lanes[above], lanes[below]) = (.upper, .lower)
                case let (.full, taken): lanes[above] = taken == .upper ? .lower : .upper
                case let (taken, .full): lanes[below] = taken == .lower ? .upper : .lower
                default: break
                }
            }
        }
        return lanes
    }
}
