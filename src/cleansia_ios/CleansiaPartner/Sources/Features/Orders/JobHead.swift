import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// What the head of a job says: its number and its status as the Core table reads it, its day and window, how far
/// along it is, where it is and what she earns. Built from the job (`OrderDetail.headFacts`), drawn by `JobHead`.
struct JobHeadFacts: Equatable {
    enum Progress: Equatable {
        case none
        case steps(step: Int, fraction: Double?, done: Bool)
        case cancelled
    }

    static let stepCount = 4

    var number = ""
    var statusWire: Int?
    var statusLabel = ""
    var day: String?
    var window: String?
    var status: [String] = []
    var progress = Progress.none
    var street: String?
    var sub: [String] = []
    var pay: String?
    var caption: String?

    var stepCounter: String? {
        guard case let .steps(step, _, _) = progress else { return nil }
        return L10n.Orders.stepCounter(step, Self.stepCount)
    }
}

/// Where the band's figure stands over the sheet: a row of the head that starts above `foot` keeps `width` clear at
/// its trailing edge, so its words wrap beside the art instead of under it. `foot` is measured from the head's top.
struct MascotClearance: Equatable {
    var width: CGFloat = 0
    var foot: CGFloat = 0

    static let none = MascotClearance()
}

private struct MascotClearanceKey: EnvironmentKey {
    static let defaultValue = MascotClearance.none
}

extension EnvironmentValues {
    var mascotClearance: MascotClearance {
        get { self[MascotClearanceKey.self] }
        set { self[MascotClearanceKey.self] = newValue }
    }
}

/// The gap above a row of a `ClearanceStack`.
struct RowGap: LayoutValueKey {
    static let defaultValue: CGFloat = 0
}

/// Rows stacked top-down, each after its own gap. A row that starts above the figure's foot is offered the width
/// less the figure's; one that starts at or below it, the whole width. Rows are settled in order, so a row's top
/// never depends on a row under it.
struct ClearanceStack: Layout {
    var clearance: MascotClearance

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache _: inout ()) -> CGSize {
        let rows = arrange(subviews, width: proposal.width)
        return CGSize(
            width: proposal.width ?? rows.map(\.frame.maxX).max() ?? 0,
            height: rows.last?.frame.maxY ?? 0
        )
    }

    func placeSubviews(in bounds: CGRect, proposal _: ProposedViewSize, subviews: Subviews, cache _: inout ()) {
        for (index, row) in arrange(subviews, width: bounds.width).enumerated() {
            subviews[index].place(
                at: CGPoint(x: bounds.minX, y: bounds.minY + row.frame.minY),
                proposal: ProposedViewSize(width: row.offered, height: nil)
            )
        }
    }

    private func arrange(_ subviews: Subviews, width: CGFloat?) -> [(frame: CGRect, offered: CGFloat?)] {
        var top: CGFloat = 0
        return subviews.enumerated().map { index, subview in
            if index > 0 { top += subview[RowGap.self] }
            let offered = width.map { top < clearance.foot ? max($0 - clearance.width, 0) : $0 }
            let size = subview.sizeThatFits(ProposedViewSize(width: offered, height: nil))
            defer { top += size.height }
            return (CGRect(origin: CGPoint(x: 0, y: top), size: size), offered)
        }
    }
}

/// The head of a job: the number and its status, the day and window, the status line with the step it is on and the
/// four-step tracker, the street and area beside the pay, then the day line and the state's finding. Its rows take
/// the band figure's clearance while they start above its foot.
struct JobHead: View {
    @Environment(\.mascotClearance) private var clearance
    let facts: JobHeadFacts
    var line: AnyView?
    var extra: AnyView?

    var body: some View {
        ClearanceStack(clearance: clearance) {
            if !facts.number.isEmpty || !facts.statusLabel.isEmpty {
                numberRow
            }
            if let day = facts.day {
                OrderCalendarLine(day, window: facts.window).layoutValue(key: RowGap.self, value: Spacing.hair)
            }
            if !facts.status.isEmpty {
                statusLine.layoutValue(key: RowGap.self, value: Spacing.s)
            }
            if facts.progress != .none {
                progress.layoutValue(key: RowGap.self, value: Spacing.xs)
            }
            if facts.street != nil || facts.pay != nil {
                factsRow.layoutValue(key: RowGap.self, value: Spacing.m)
            }
            if let line {
                line.layoutValue(key: RowGap.self, value: Spacing.m)
            }
            if let extra {
                extra.layoutValue(key: RowGap.self, value: Spacing.s)
            }
        }
    }

    private var numberRow: some View {
        ViewThatFits(in: .horizontal) {
            HStack(alignment: .center, spacing: Spacing.xs) {
                number
                badge
            }
            VStack(alignment: .leading, spacing: Spacing.xxs) {
                number
                badge
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    @ViewBuilder
    private var number: some View {
        if !facts.number.isEmpty {
            numberText
        }
    }

    private var numberText: some View {
        Text(verbatim: facts.number)
            .font(CleansiaTypography.titleMedium)
            .monospacedDigit()
            .foregroundColor(CleansiaColors.onSurface)
            .fixedSize(horizontal: false, vertical: true)
            .accessibilityAddTraits(.isHeader)
    }

    private var badge: some View {
        OrderStatusBadge(
            label: facts.statusLabel,
            tint: OrderStatusStyle.tint(wireValue: facts.statusWire),
            tone: OrderStatusStyle.tone(wireValue: facts.statusWire)
        )
        .fixedSize()
    }

    private var statusLine: some View {
        HStack(alignment: .lastTextBaseline, spacing: Spacing.s) {
            OrdersDotFlow(items: facts.status.map { Text($0) }, style: CleansiaTypography.scale.titleLarge)
                .foregroundColor(CleansiaColors.onSurface)
                .frame(maxWidth: .infinity, alignment: .leading)
            if let counter = facts.stepCounter {
                Text(counter)
                    .font(CleansiaTypography.labelSmall)
                    .monospacedDigit()
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .fixedSize()
            }
        }
    }

    @ViewBuilder
    private var progress: some View {
        switch facts.progress {
        case let .steps(step, fraction, done):
            OrderTrackerBar(
                state: OrderTrackerRule.state(
                    step: step - 1,
                    cancelled: false,
                    completed: done,
                    stepCount: JobHeadFacts.stepCount
                ),
                cancelledLabel: "",
                currentFraction: fraction
            )
            .accessibilityHidden(true)
        case .cancelled:
            Capsule().fill(CleansiaColors.error).frame(height: Spacing.xxs).accessibilityHidden(true)
        case .none:
            EmptyView()
        }
    }

    private var factsRow: some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            VStack(alignment: .leading, spacing: Spacing.hair) {
                if let street = facts.street {
                    Text(street)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurface)
                        .fixedSize(horizontal: false, vertical: true)
                }
                subLine
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            if let pay = facts.pay {
                VStack(alignment: .trailing, spacing: Spacing.hair) {
                    Text(pay)
                        .font(CleansiaTypography.titleMedium)
                        .monospacedDigit()
                        .foregroundColor(CleansiaColors.onSurface)
                        .lineLimit(1)
                        .padding(.horizontal, Spacing.s)
                        .padding(.vertical, 6)
                        .background(CleansiaColors.primary.opacity(0.12), in: Capsule())
                    if let caption = facts.caption {
                        Text(caption)
                            .font(CleansiaTypography.labelMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                            .multilineTextAlignment(.trailing)
                    }
                }
                .fixedSize()
            }
        }
    }

    @ViewBuilder
    private var subLine: some View {
        if !facts.sub.isEmpty {
            OrdersDotFlow(
                items: facts.sub.map { Text($0) },
                style: CleansiaTypography.scale.bodyMedium,
                tiesLast: true
            )
            .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
    }
}

/// The head's day line on the detail, before the work starts: a job not taken, Confirmed, on the way, a cancelled one.
/// While she cleans the tracker's current segment fills with the booked window instead, and once done there is none.
/// It draws her other jobs that day only where they answer something: beside a job not taken, and on the day a
/// cancellation freed.
struct OrderDetailTimeline: View {
    @Environment(\.locale) private var locale
    let order: OrderDetail
    let now: Date
    let ownRows: [OrderListItem]?
    let clash: OrderListItem?
    let isMine: Bool

    static func shows(_ order: OrderDetail, isMine: Bool) -> Bool {
        guard order.cleaningDateTime != nil, order.bookedEnd != nil, order.status != ._5 else { return false }
        return !(order.status == ._4 && isMine)
    }

    var body: some View {
        if Self.shows(order, isMine: isMine), let start = order.cleaningDateTime, let end = order.bookedEnd {
            dayLine(start: start, end: end)
        }
    }

    private func dayLine(start: Date, end: Date) -> some View {
        let calendar = Calendar.current
        let job = OrdersBoardRules.minuteSpan(start: start, end: end)
        let today = calendar.isDate(start, inSameDayAs: now)
        let nowMinute = today ? OrdersBoardRules.minuteSpan(start: now, end: now).start : nil
        let sameDay = (ownRows ?? []).filter { row in
            row.id != order.id && row.cleaningDateTime.map { calendar.isDate($0, inSameDayAs: start) } == true
        }
        var mine: [(start: Int, end: Int)] = []
        var open: [(start: Int, end: Int)] = []
        var clashes: [(start: Int, end: Int)] = []
        var band: (start: Int, end: Int)?
        var labels: [OrdersDayLine.Label] = []
        if let nowMinute {
            labels.append(.init(minute: nowMinute, text: OrdersFormat.timeOnly(now, locale: locale)))
        }
        if order.status == ._6 {
            mine = sameDay.filter { (OrdersBoardRules.bookedEnd($0) ?? .distantPast) > now }.compactMap(span)
            if max(start, now) < end { open = [OrdersBoardRules.minuteSpan(start: max(start, now), end: end)] }
            if start > now, let window = OrdersBoardRules.window(
                start: start,
                minutes: order.estimatedMinutes,
                locale: locale
            ) {
                labels.append(.init(
                    minute: (job.start + job.end) / 2,
                    text: "\(window) \(L10n.Orders.dayLineFree)",
                    isSetOff: true
                ))
            }
        } else if isMine {
            mine = [job]
            if order.status == ._2 {
                let opensAt = start.addingTimeInterval(-TimeInterval(OrdersBoardRules.startGraceWindowMinutes) * 60)
                band = (job.start - OrdersBoardRules.startGraceWindowMinutes, job.start)
                if !today || now < opensAt {
                    labels.append(.init(
                        minute: job.start - OrdersBoardRules.startGraceWindowMinutes,
                        text: OrdersFormat.timeOnly(opensAt, locale: locale),
                        isSetOff: true
                    ))
                }
            }
        } else {
            mine = sameDay.compactMap(span)
            open = [job]
            if let clash, let clashStart = clash.cleaningDateTime, let clashEnd = OrdersBoardRules.bookedEnd(clash) {
                clashes = [OrdersBoardRules.minuteSpan(start: max(start, clashStart), end: min(end, clashEnd))]
            }
        }
        return OrdersDayLine(
            range: OrdersBoardRules.dayRange(mine + open + (band.map { [$0] } ?? [])),
            mine: mine,
            open: open,
            clashes: clashes,
            setOff: band,
            now: nowMinute,
            labels: labels
        )
    }

    private func span(_ row: OrderListItem) -> (start: Int, end: Int)? {
        guard let start = row.cleaningDateTime, let end = OrdersBoardRules.bookedEnd(row) else { return nil }
        return OrdersBoardRules.minuteSpan(start: start, end: end)
    }
}

extension OrdersBoardRules {
    /// The status line of a job she has not taken: how far off its day is ("Today", "Tomorrow", "In 3 days"), never the
    /// weekday, which is the calendar line's; a job already under way says so.
    static func howFarOff(_ start: Date, live: Bool, now: Date, locale: Locale) -> [String] {
        if live { return [L10n.Orders.dayToday, L10n.Orders.underWayShort] }
        let calendar = Calendar.current
        let days = calendar.dateComponents(
            [.day],
            from: calendar.startOfDay(for: now),
            to: calendar.startOfDay(for: start)
        ).day ?? 0
        switch days {
        case ..<1: return [L10n.Orders.dayToday]
        case 1: return [L10n.Orders.dayTomorrow]
        default:
            let far = L10n.Orders.inDays(days)
            return [far.prefix(1).uppercased(with: locale) + far.dropFirst()]
        }
    }
}
